namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Asks the by-ISBN source once for a subtitle for each volume a catalogue already named
/// without one. Writes nothing else, and never over a subtitle that is already there.
/// </summary>
// Why this is a command and not a change to the nightly pass. Volume.Title's "" means "a
// source named this volume and it has no subtitle", and enrichment treats it as resolved on
// purpose: whole series legitimately have no volume subtitles, so re-asking about "" would
// cost a lookup per volume per night forever for an answer already given. The catch is that
// the store catalogue is first in precedence and measurably never carries one — 0 of 282
// live Apple titles say anything after the volume number — so every volume the store reaches
// lands on "", and Open Library, which does carry them ("Run Through the Battlefield"), is
// then never asked. Making "" re-askable would burn ~72 lookups a night for good. Asking
// once, deliberately, costs ~72 lookups in total, which is what this is.
//
// Not idempotent in the strict sense, and it cannot be made so without inventing a state.
// A volume whose Open Library record has no subtitle stays "" and is asked again by the next
// run of this command — the eligible set shrinks to the volumes nothing has a subtitle for
// and then stops shrinking. That is accepted for a hand-run one-shot: a fourth Title state,
// or an "Open Library was asked too" column, would be a marker in the shelf's data existing
// only to serve a command meant to be run twice at most. The nightly sync is unaffected
// either way — it still never re-asks about "".
public sealed class SubtitleBackfillService(
    LnShelfDbContext dbContext,
    IVolumeEnrichmentSource enrichmentSource,
    ILogger<SubtitleBackfillService> logger)
{
    public async Task<SubtitleBackfillReport> RunAsync(CancellationToken cancellationToken)
    {
        var targets = await LoadTargetsAsync(cancellationToken);

        // No LookupBudget, unlike the sync run's passes, and for the reason they need one:
        // enrichment re-asks about its gaps every night, so an uncapped nightly run could
        // spend an hour. This asks each volume exactly once and then has nothing left to
        // ask, so the eligible count *is* the cost — printed up front so whoever ran it
        // knows what they started. The politeness pacing is already in the client's handler
        // chain (twelve requests a minute), so nothing here has to sleep.
        logger.LogInformation(
            "Subtitle backfill: {VolumeCount} volume(s) across {SeriesCount} series, one {SourceName} "
            + "lookup each",
            targets.Sum(target => target.Volumes.Count),
            targets.Count,
            enrichmentSource.SourceName);

        var results = new List<SubtitleBackfillSeriesResult>(targets.Count);

        foreach (var target in targets)
        {
            results.Add(await BackfillAsync(target, cancellationToken));

            // Saved per series, where IsbnApplyService saves once at the end. Its work is
            // local and instant; this spends a live lookup per volume at twelve a minute, so
            // a full run is minutes long and a Ctrl-C partway through must not throw away
            // the subtitles the earlier series already learned.
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var report = new SubtitleBackfillReport { Series = results };

        logger.LogInformation(
            "Subtitle backfill finished: {Asked} asked, {Subtitled} subtitled, {WithoutSubtitle} with no "
            + "subtitle, {NotIndexed} not indexed, {Failed} failed",
            report.Asked,
            report.Subtitled,
            report.WithoutSubtitle,
            report.NotIndexed,
            report.Failed);

        return report;
    }

    /// <summary>
    /// Volumes with an ISBN whose title says "named, and it has no subtitle".
    /// </summary>
    // The filter is the whole safety argument, so it is worth stating what each half keeps
    // out. Null titles are left to the nightly pass: they are the unresolved ones, it already
    // asks about them, and stealing them would only mean this command doing the sync's job
    // more expensively. Non-empty titles are left alone because they are real subtitles, and
    // first-wins is the rule that keeps two sources from overwriting each other forever
    // (VolumeFactWriter). A Japanese-market ISBN is skipped exactly as the sync skips it —
    // it resolves to a Japanese edition, whose title is the wrong title for an English shelf
    // entry — and from the ISBN rather than the record, because the record may state no
    // language at all.
    private async Task<List<BackfillTarget>> LoadTargetsAsync(CancellationToken cancellationToken)
    {
        // Queried off Volumes rather than as a filtered Include on Series, which is the more
        // obvious shape and the wrong one: a filtered Include is fixed up with every instance
        // of the navigation the context already tracks, so a second run in one process would
        // find the volumes the first run had just written sitting in the collection again.
        // Asking for the rows directly answers with exactly the rows that match.
        //
        // Tracked, unlike the discovery command's query: these volumes are what gets written.
        var volumes = await dbContext.Volumes
            .Where(volume => volume.Isbn != null && volume.Title == "")
            .ToListAsync(cancellationToken);

        // In memory, because the rule is a C# one — the registration group has to be read out
        // of the digits (Isbn.IsJapaneseEdition), and there is no translation of that.
        var eligible = volumes.Where(volume => !Isbn.IsJapaneseEdition(volume.Isbn)).ToList();

        var seriesIds = eligible.Select(volume => volume.SeriesId).ToHashSet();

        var titles = await dbContext.Series
            .Where(series => seriesIds.Contains(series.Id))
            .Select(series => new { series.Id, series.TitleEnglish })
            .ToDictionaryAsync(series => series.Id, series => series.TitleEnglish, cancellationToken);

        return
        [
            .. eligible
                .GroupBy(volume => volume.SeriesId)
                .Select(group => new BackfillTarget
                {
                    SeriesTitle = titles[group.Key],
                    Volumes = [.. group.OrderBy(volume => volume.Number)],
                })
                .OrderBy(target => target.SeriesTitle, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private async Task<SubtitleBackfillSeriesResult> BackfillAsync(
        BackfillTarget target,
        CancellationToken cancellationToken)
    {
        var outcomes = new List<SubtitleOutcome>(target.Volumes.Count);

        foreach (var volume in target.Volumes)
        {
            outcomes.Add(await LookUpAsync(target.SeriesTitle, volume, cancellationToken));
        }

        return new SubtitleBackfillSeriesResult
        {
            SeriesTitle = target.SeriesTitle,
            Asked = target.Volumes.Count,
            Subtitled = outcomes.Count(outcome => outcome is SubtitleOutcome.Subtitled),
            WithoutSubtitle = outcomes.Count(outcome => outcome is SubtitleOutcome.WithoutSubtitle),
            NotIndexed = outcomes.Count(outcome => outcome is SubtitleOutcome.NotIndexed),
            Failed = outcomes.Count(outcome => outcome is SubtitleOutcome.Failed),
        };
    }

    private async Task<SubtitleOutcome> LookUpAsync(
        string seriesTitle,
        Volume volume,
        CancellationToken cancellationToken)
    {
        // Per-volume failure isolation, the rule the sync run and the discovery command both
        // follow: one unreachable edition record must not cost a series its other fourteen
        // volumes, and a run that dies at volume three has spent its lookups for nothing.
        try
        {
            var enrichment = await enrichmentSource.GetByIsbnAsync(volume.Isbn!, cancellationToken);

            if (enrichment is null)
            {
                // Not an error, and the most common miss by a distance: the printing the
                // reader owns is often simply not the printing a library catalogued.
                logger.LogInformation(
                    "{SourceName} has no record for {SeriesTitle} volume {VolumeNumber} (ISBN {Isbn})",
                    enrichmentSource.SourceName,
                    seriesTitle,
                    volume.Number,
                    volume.Isbn);

                return SubtitleOutcome.NotIndexed;
            }

            if (enrichment.Language is EditionLanguage.Other)
            {
                // The ISBN check in the query catches the Japanese-market ones; this catches
                // the rest — a Brazilian or French edition sharing a work with the English
                // one, whose title is not this shelf entry's title.
                logger.LogInformation(
                    "Skipped {RecordKey} for {SeriesTitle} volume {VolumeNumber}: the record names a "
                    + "language other than English",
                    enrichment.SourceRecordKey,
                    seriesTitle,
                    volume.Number);

                return SubtitleOutcome.NotIndexed;
            }

            return ApplySubtitle(seriesTitle, volume, enrichment);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Subtitle lookup failed for {SeriesTitle} volume {VolumeNumber} (ISBN {Isbn}); continuing",
                seriesTitle,
                volume.Number,
                volume.Isbn);

            return SubtitleOutcome.Failed;
        }
    }

    /// <summary>
    /// Writes the subtitle, where the record turns out to carry one.
    /// </summary>
    // Deliberately not routed through VolumeFactWriter, although the rule being applied is
    // that class's own null → "" → subtitle upgrade. ApplyAsync writes four fields from a
    // record: it would also move release dates, fill blurbs and pull cover art, and it needs
    // a SyncRun to record discrepancies into and an IVolumeCoverCache to fetch with. This
    // command promises to touch one column, and a subtitle pass that quietly cached covers
    // would be a worse command and a harder one to trust. What is left is the same rule with
    // nothing attached: the eligible set is exactly the volumes whose title is "", and only a
    // non-empty subtitle is ever written, so this can move "" → subtitle and nothing else.
    //
    // The extractor is shared with the writer rather than reimplemented, because "what the
    // Title column holds" is a rule about the shelf and there must be one copy of it.
    private SubtitleOutcome ApplySubtitle(string seriesTitle, Volume volume, VolumeEnrichment enrichment)
    {
        var subtitle = VolumeSubtitleExtractor.Extract(enrichment.Title, volume.Number);

        if (subtitle.Length is 0)
        {
            // The record named the volume and it has no subtitle — which is what "" already
            // says, so there is nothing to write. Two sources now agree about this volume.
            return SubtitleOutcome.WithoutSubtitle;
        }

        volume.Title = subtitle;

        logger.LogInformation(
            "{SeriesTitle} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\") subtitles it "
            + "\"{Subtitle}\"",
            seriesTitle,
            volume.Number,
            enrichment.SourceRecordKey,
            enrichment.Title,
            subtitle);

        return SubtitleOutcome.Subtitled;
    }

    /// <summary>What one volume's lookup came to.</summary>
    private enum SubtitleOutcome
    {
        Subtitled,
        WithoutSubtitle,
        NotIndexed,
        Failed,
    }

    /// <summary>One series' eligible volumes, with the title the report prints.</summary>
    private sealed record BackfillTarget
    {
        public required string SeriesTitle { get; init; }

        /// <summary>Tracked entities: these are what the run writes to.</summary>
        public required IReadOnlyList<Volume> Volumes { get; init; }
    }
}
