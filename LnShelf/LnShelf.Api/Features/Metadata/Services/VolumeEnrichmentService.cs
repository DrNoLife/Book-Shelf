namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Applies one enrichment source's per-volume facts to a series' volumes: a date, a
/// cover, a title, and a recorded disagreement where the sources do not agree.
/// </summary>
// Extracted from SeriesSyncService rather than added to it. That class's job is "walk
// the shelf, reconcile each series against the release source"; this one's is "read one
// volume's edition record and decide what may be written from it", which is where all
// the conflict rules are. Two jobs, two classes.
//
// What it never does: write user state, or touch VolumeUserState.CoverOverrideObjectKey.
// Enrichment only ever writes Volume.*, which is what keeps "sync never overwrites user
// state" and "overrides always win" true by construction.
public sealed class VolumeEnrichmentService(
    IVolumeEnrichmentSource enrichmentSource,
    IIsbnCandidateSource candidateSource,
    IVolumeCoverCache coverCache,
    ILogger<VolumeEnrichmentService> logger)
{
    /// <param name="today">
    /// "In the future" is relative to a day, so the caller supplies it — the same
    /// convention the projections follow.
    /// </param>
    /// <param name="budget">
    /// Spent down as requests are made, not as volumes are visited: one volume can cost
    /// several requests when its own ISBN turns out not to be indexed.
    /// </param>
    public async Task EnrichAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(budget);

        foreach (var volume in series.Volumes.OrderBy(volume => volume.Number))
        {
            if (budget.IsExhausted)
            {
                break;
            }

            if (!ShouldLookUp(volume))
            {
                continue;
            }

            // Per-volume failure isolation. The sync run already isolates per series;
            // this keeps one unreachable edition record from costing a series its other
            // twenty volumes.
            try
            {
                await ApplyAsync(series, volume, run, today, budget, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                run.EnrichmentFailures++;
                logger.LogWarning(
                    exception,
                    "Enrichment failed for {TitleEnglish} volume {VolumeNumber} (ISBN {Isbn}); continuing",
                    series.TitleEnglish,
                    volume.Number,
                    volume.Isbn);
            }
        }
    }

    /// <summary>
    /// Whether this volume is worth a request. Silent about the ones that are not.
    /// </summary>
    // Three reasons to skip, and none of them is an error:
    //
    // - No ISBN. The seam is keyed on ISBN because that is the only key that works; a
    //   volume without one has nothing to ask about. 41% of the owned shelf, and the
    //   discovery command is the answer to it, not this.
    // - A Japanese-market ISBN. It resolves to a Japanese-market edition, whose date is
    //   the Japanese release date and whose art is the Japanese cover — both wrong on an
    //   English shelf entry. Checked from the ISBN rather than from the record, because
    //   the record may state no language at all: Slime volume 1's Japanese ISBN resolves
    //   to a French edition with an empty `languages` field.
    // - Already complete. A volume with a date, a cover and a title has nothing left to
    //   learn, so a nightly run does not re-ask. Incomplete ones are re-asked, which is
    //   deliberate: Open Library's catalogue grows, and today's missing record is next
    //   quarter's hit.
    private static bool ShouldLookUp(Volume volume) =>
        volume.Isbn is not null
        && !Isbn.IsJapaneseEdition(volume.Isbn)
        && (volume.ReleaseDate is null || volume.CoverObjectKey is null || volume.Title is null);

    private async Task ApplyAsync(
        Series series,
        Volume volume,
        SyncRun run,
        DateOnly today,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        budget.Spend(1);
        var enrichment = await enrichmentSource.GetByIsbnAsync(volume.Isbn!, cancellationToken);

        // Not indexed under the ISBN this shelf holds — which does not mean the volume is
        // absent. The printing the reader owns is often simply not the printing a library
        // catalogued: five of Slime's ISBNs 404 while Open Library plainly holds those
        // volumes under other ISBNs. So the volume is searched for by name, and any
        // candidate has to prove it is this series' volume N before it is believed.
        enrichment ??= await ResolveBySearchAsync(series, volume, run, budget, cancellationToken);

        if (enrichment is null)
        {
            run.EnrichmentGaps++;
            return;
        }

        if (enrichment.Language is EditionLanguage.Other)
        {
            // The ISBN check above catches the Japanese-market ones; this catches the
            // rest — a Brazilian or French edition sharing a work with the English one.
            run.EnrichmentGaps++;
            logger.LogInformation(
                "Skipped {RecordKey} for {TitleEnglish} volume {VolumeNumber}: the record names a "
                + "language other than English",
                enrichment.SourceRecordKey,
                series.TitleEnglish,
                volume.Number);
            return;
        }

        run.VolumesEnriched++;

        ApplyReleaseDate(series, volume, enrichment, run, today);
        ApplyTitle(volume, enrichment);
        await ApplyCoverAsync(volume, enrichment, run, cancellationToken);
    }

    /// <summary>
    /// Finds another printing of the same volume, when the held ISBN is not indexed.
    /// </summary>
    // Held to a far higher bar than a direct ISBN lookup, and deliberately so. Asking for
    // an ISBN is unambiguous; searching by name is a guess, and this shelf has the worst
    // case of it — the Slime light novel and the Slime manga share a title. So a candidate
    // is only believed when its own record names this series, this volume number and the
    // right medium (VolumeEditionMatcher), and the first one that does wins.
    //
    // Volume.Isbn is never rewritten from here. It records the edition the reader owns; the
    // record that happened to supply a date is not that.
    private async Task<VolumeEnrichment?> ResolveBySearchAsync(
        Series series,
        Volume volume,
        SyncRun run,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted)
        {
            return null;
        }

        budget.Spend(1);
        var candidates = await candidateSource.FindAsync(
            series.TitleEnglish, volume.Number, cancellationToken);

        foreach (var candidate in candidates.Isbns)
        {
            if (budget.IsExhausted)
            {
                return null;
            }

            // The reader's own ISBN was already asked for and is what got us here.
            if (string.Equals(candidate, volume.Isbn, StringComparison.Ordinal)
                || Isbn.IsJapaneseEdition(candidate))
            {
                continue;
            }

            budget.Spend(1);
            var enrichment = await enrichmentSource.GetByIsbnAsync(candidate, cancellationToken);

            if (enrichment is null
                || enrichment.Language is EditionLanguage.Other
                || !VolumeEditionMatcher.Matches(series.TitleEnglish, volume.Number, series.Format, enrichment))
            {
                continue;
            }

            // The record has to say which medium it is, not merely fail to say it is the
            // other one. This path writes to the shelf with nobody looking, and the case it
            // guards against is real: a discovery run over this shelf proposed three
            // Kodansha manga volumes for a Yen Press light novel, all of them titled just
            // "…Slime 18" with no marker at all. Where Open Library does not say, this does
            // not guess — the volume is left alone and the review file is the way in.
            if (!VolumeEditionMatcher.ConfirmsMedium(series.Format, enrichment))
            {
                logger.LogInformation(
                    "{TitleEnglish} volume {VolumeNumber}: {RecordKey} (\"{RecordTitle}\", {Publisher}) is the "
                    + "right volume but does not state that it is a {Format}; left for review",
                    series.TitleEnglish,
                    volume.Number,
                    enrichment.SourceRecordKey,
                    enrichment.Title,
                    enrichment.Publisher,
                    series.Format);

                continue;
            }

            logger.LogInformation(
                "{TitleEnglish} volume {VolumeNumber}: ISBN {Isbn} is not indexed, but {RecordKey} "
                + "({CandidateIsbn}, \"{RecordTitle}\") is the same volume",
                series.TitleEnglish,
                volume.Number,
                volume.Isbn,
                enrichment.SourceRecordKey,
                candidate,
                enrichment.Title);

            run.VolumesResolvedBySearch++;

            return enrichment;
        }

        return null;
    }

    /// <summary>
    /// Writes the date, unless it contradicts what the release source already said.
    /// </summary>
    // The rule the verdict settled (§1). A real date wins for display where one exists,
    // but IsReleased stays the release model: it is a sourced count covering the whole
    // shelf, where dates cover 63% of the minority of volumes whose ISBN is known.
    //
    // So a date in the past is written — it agrees with IsReleased and says more than it
    // does. A future date on a volume the release source has already counted as in print
    // is the two sources contradicting each other about one volume, and the standing rule
    // for that is "recorded, never silently picked": the disagreement becomes a row for a
    // person and the existing value is left exactly as it was. Writing it would put a
    // future date on a volume the shelf simultaneously reports as out, which is the
    // silent inconsistency the rule exists to prevent.
    private void ApplyReleaseDate(
        Series series,
        Volume volume,
        VolumeEnrichment enrichment,
        SyncRun run,
        DateOnly today)
    {
        if (enrichment.ReleaseDate is not { } releaseDate)
        {
            if (enrichment.RawPublishDate is { } raw)
            {
                // Said out loud: "2022" is a real fact that is not a date, and going
                // quiet about it would look like the record had none at all.
                logger.LogInformation(
                    "{RecordKey} dates {TitleEnglish} volume {VolumeNumber} only as {RawPublishDate}, "
                    + "which names no day",
                    enrichment.SourceRecordKey,
                    series.TitleEnglish,
                    volume.Number,
                    raw);
            }

            return;
        }

        if (releaseDate == volume.ReleaseDate)
        {
            return;
        }

        if (releaseDate > today && volume.IsReleased)
        {
            run.Discrepancies.Add(new SyncDiscrepancy
            {
                Id = Guid.CreateVersion7(),
                SeriesId = series.Id,
                SeriesTitle = series.TitleEnglish,
                Field = SyncDiscrepancyField.ReleaseDate,
                VolumeNumber = volume.Number,
                ResolvedWith = null,
                Note = $"ISBN {volume.Isbn}, record {enrichment.SourceRecordKey}. "
                       + "The date was not written; the count still decides whether this volume is out.",
            }
                .Says(MetadataSourceName.MangaUpdates, "in print")
                .Says(
                    MetadataSourceName.OpenLibrary,
                    releaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            logger.LogInformation(
                "{TitleEnglish} volume {VolumeNumber}: the release source says in print, {SourceName} says "
                + "{ReleaseDate}. Recorded for review; nothing written",
                series.TitleEnglish,
                volume.Number,
                enrichmentSource.SourceName,
                releaseDate);

            return;
        }

        volume.ReleaseDate = releaseDate;
        run.DatesMoved++;
    }

    private void ApplyTitle(Volume volume, VolumeEnrichment enrichment)
    {
        if (enrichment.Title is not { } title || title == volume.Title)
        {
            return;
        }

        // Metadata, so the source's current answer wins over the last one it gave. Only
        // Volume.Title is written; nothing here can reach a user-editable field.
        volume.Title = title;
    }

    /// <summary>
    /// Caches the art under this volume's synced key, leaving any override untouched.
    /// </summary>
    // The override is not even consulted: an override lives on VolumeUserState under a
    // different key prefix, and the projection prefers it on read. Writing the synced key
    // while an override exists is correct and wanted — feature-spec §4.3 says removing an
    // override falls back to synced art, which requires the synced art to be there.
    private async Task ApplyCoverAsync(
        Volume volume,
        VolumeEnrichment enrichment,
        SyncRun run,
        CancellationToken cancellationToken)
    {
        if (enrichment.CoverImageUrl is not { } coverImageUrl || volume.CoverObjectKey is not null)
        {
            return;
        }

        var objectKey = await coverCache.EnsureCachedAsync(
            volume.Id, enrichmentSource.SourceName, coverImageUrl, cancellationToken);

        if (objectKey is null)
        {
            return;
        }

        volume.CoverObjectKey = objectKey;
        run.VolumeCoversCached++;
    }
}
