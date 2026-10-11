namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Refreshes one series against the release source and the enrichment source: links it,
/// applies the volume count, and enriches whatever volumes carry an ISBN.
/// </summary>
// Extracted from SeriesSyncService when adding a series started needing the same work.
// The nightly sweep and a single on-demand refresh must do *identically* the same thing to
// one series — feature-spec §4.2 says "same code path" about exactly this — and the way to
// guarantee that is for there to be only one copy of it.
//
// It deliberately does not create or save a SyncRun. Its caller owns the run, because
// "what a run is" differs: the sweep is every series, an on-demand refresh is one.
public sealed class SeriesRefresher(
    IReleaseSourceMatcher releaseSourceMatcher,
    IReleaseSource releaseSource,
    ISeriesMetadataSource metadataSource,
    ReleaseCountWriter releaseCountWriter,
    PublicationStatusWriter publicationStatusWriter,
    VolumeEnrichmentService volumeEnrichment,
    TimeProvider timeProvider,
    ILogger<SeriesRefresher> logger)
{
    /// <param name="linkedExternalIds">
    /// Release-source ids already spoken for, so two series cannot link to one record.
    /// Added to as links are made.
    /// </param>
    /// <returns>
    /// What this refresh changed that a reader would call news (ShelfEventDiff). Not yet
    /// added to anything: the caller owns the save, so it owns these too.
    /// </returns>
    public async Task<IReadOnlyList<ShelfEvent>> RefreshAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(linkedExternalIds);
        ArgumentNullException.ThrowIfNull(budget);

        // Read before anything below writes it. A series' *first* refresh is the one that
        // runs the moment it is added, and its whole backlist arriving then is not news —
        // see ReleaseCountWriter.
        var refreshedBefore = series.LastSyncedAt is not null;

        // Taken before anything is written, for the same reason; null on a first refresh.
        var snapshot = ShelfEventDiff.Capture(series, timeProvider.LocalTimeZone);

        var info = series.MangaUpdatesId is { } externalId
            ? await releaseSource.GetSeriesAsync(externalId, cancellationToken)
            : await LinkAsync(series, run, linkedExternalIds, cancellationToken);

        if (info is not null)
        {
            releaseCountWriter.Apply(series, info, run, refreshedBefore);
            publicationStatusWriter.Apply(series, info, run);
        }

        // Enrichment runs whether or not the release source had anything to say: it reads
        // per-volume ISBNs, which have nothing to do with whether this series is linked.
        // Ordered after the count so a volume the count just created can be enriched in
        // the same pass.
        await EnrichVolumesAsync(series, run, budget, info?.ExtraVolumeCount ?? 0, cancellationToken);

        // Also unconditional, and for the same reason: whether this series has a synopsis
        // has nothing to do with whether the release source recognised it.
        await HealDescriptionAsync(series, cancellationToken);

        // Before the early return below: enrichment can date a volume of a series whose
        // release-source record came back empty, and that date passing is still news.
        var events = snapshot is null
            ? []
            : ShelfEventDiff.Compare(snapshot, series, timeProvider.Today(), timeProvider.GetUtcNow());

        if (info is null)
        {
            return events;
        }

        // After the enrichment, so it reads the dates as this run left them.
        ReleaseCountWriter.RecordChosenDateConflicts(series, info, run, timeProvider.Today());

        // Metadata. The single exception in this class is HasNewVolume (see ReleaseCountWriter);
        // no other SeriesUserState or VolumeUserState field is ever written here, because
        // sync does not own user state (CLAUDE.md).
        series.LastSyncedAt = timeProvider.GetUtcNow();

        return events;
    }

    private async Task EnrichVolumesAsync(
        Series series,
        SyncRun run,
        LookupBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted)
        {
            return;
        }

        var today = timeProvider.Today();

        // The budget is handed over rather than a number: one volume can cost several
        // requests when its own ISBN is not the one that got indexed, and only the
        // enrichment knows how many it actually spent.
        await volumeEnrichment.EnrichAsync(series, run, today, budget, expectedBonusVolumes, cancellationToken);
    }

    /// <summary>
    /// Gives a series a synopsis when it has none — the series added before the shelf
    /// ever asked AniList for one.
    /// </summary>
    // The description only, never the rest of the metadata. Re-applying every AniList field
    // here would make a gap-fill indistinguishable from a full re-sync, and the other fields
    // were already written when the series was added.
    //
    // No budget mechanics, deliberately, unlike the enrichment above. This costs one AniList
    // request per description-less series and is self-extinguishing — the guard stops firing
    // the moment the column is filled. AniList allows 30 requests a minute and this shelf
    // holds single digits of series, so the worst case is one sweep spending nine of them,
    // once. A LookupBudget here would be machinery guarding nothing.
    //
    // Self-extinguishing only because an answer of "no synopsis" is written down too, as the
    // empty string — the same three states Volume.Title uses: null is "never asked", empty is
    // "asked, and AniList has none". Left null, a series AniList carries no synopsis for would
    // be asked about every night forever. The cost is that a synopsis somebody adds to AniList
    // later does not arrive on its own; clearing the column to null asks again. Every reader
    // treats blank as absent (SeriesDetailProjection's NullIfBlank), so nothing displays it.
    private async Task HealDescriptionAsync(Series series, CancellationToken cancellationToken)
    {
        if (series.Description is not null || series.AniListId is not { } aniListId)
        {
            return;
        }

        try
        {
            var metadata = await metadataSource.GetSeriesAsync(aniListId, cancellationToken);

            if (metadata?.Description is not { } description)
            {
                // A failure throws and never reaches here, so this is AniList answering — with
                // no synopsis, or with no such series — and the answer is final for this row.
                series.Description = string.Empty;
                return;
            }

            series.Description = description;

            logger.LogInformation(
                "{TitleEnglish}: description filled in from AniList {AniListId}",
                series.TitleEnglish,
                aniListId);
        }
        // The caller's cancellation only: a timed-out request is a cancelled task nobody
        // asked for, and it is exactly the failure this catch exists to absorb.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // This file's failure philosophy, applied one level down: a refresh that got the
            // volume counts right is not worth failing because a synopsis could not be
            // fetched. The next run will try again — the column is still null, so the guard is
            // still true.
            logger.LogWarning(
                exception,
                "Could not fetch a description for {TitleEnglish} (AniList {AniListId}); continuing",
                series.TitleEnglish,
                aniListId);
        }
    }

    private async Task<ReleaseSeriesInfo?> LinkAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        CancellationToken cancellationToken)
    {
        var match = await releaseSourceMatcher.MatchAsync(series, linkedExternalIds, cancellationToken);

        if (!match.IsLinked)
        {
            // Recorded rather than retried silently: an unmatched series is a
            // question for a person, and this row is how it gets asked.
            run.Discrepancies.Add(SyncDiscrepancies.SeriesLink(series, match));

            return null;
        }

        series.MangaUpdatesId = match.MatchedExternalId;
        linkedExternalIds.Add(match.MatchedExternalId!.Value);
        run.SeriesLinked++;

        return match.MatchedSeries;
    }
}
