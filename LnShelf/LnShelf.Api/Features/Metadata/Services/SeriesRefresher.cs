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
    SeriesDescriptionHealer descriptionHealer,
    ReleaseCountWriter releaseCountWriter,
    PublicationStatusWriter publicationStatusWriter,
    VolumeEnrichmentService volumeEnrichment,
    ChapterMapRefresher chapterMapRefresher,
    TimeProvider timeProvider)
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
        EnrichmentBudget budget,
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
        var today = timeProvider.Today();

        var info = series.MangaUpdatesId is { } externalId
            ? await releaseSource.GetSeriesAsync(externalId, cancellationToken)
            : await LinkAsync(series, run, linkedExternalIds, cancellationToken);

        if (info is not null)
        {
            releaseCountWriter.Apply(series, info, run, refreshedBefore, today);
            publicationStatusWriter.Apply(series, info, run);

            // Metadata, never the reader's chapter (that is user state). Only a value is
            // written: a record that lists no chapter tonight must not erase last night's.
            if (info.LatestChapter is { } latestChapter)
            {
                series.LatestChapter = latestChapter;
            }
        }

        // Enrichment runs whether or not the release source had anything to say: it reads
        // per-volume ISBNs, which have nothing to do with whether this series is linked.
        // Ordered after the count so a volume the count just created can be enriched in
        // the same pass.
        var nextVolumes = await EnrichVolumesAsync(
            series, run, today, budget, info?.ExtraVolumeCount ?? 0, cancellationToken);

        // Also unconditional, and for the same reason: whether this series has a synopsis
        // has nothing to do with whether the release source recognised it.
        await descriptionHealer.HealAsync(series, cancellationToken);

        // After the count and the enrichment, so it judges a map against the volumes and the
        // latest chapter as this run left them. It isolates its own failures.
        await chapterMapRefresher.RefreshAsync(series, run, cancellationToken);

        // Before the early return below: enrichment can date a volume of a series whose
        // release-source record came back empty, and that date passing is still news.
        var events = snapshot is null
            ? []
            : ShelfEventDiff.Compare(snapshot, series, today, timeProvider.GetUtcNow());

        if (refreshedBefore && IsNewVolumeOut(snapshot, events, nextVolumes, today, releaseSourceAnswered: info is not null))
        {
            NewVolumeFlag.Raise(series);
        }

        if (info is null)
        {
            return events;
        }

        // After the enrichment, so it reads the dates as this run left them.
        ReleaseCountWriter.RecordChosenDateConflicts(series, info, run, today);

        // Metadata. The single exception in this class is HasNewVolume (ReleaseCountWriter and
        // the store's next volumes, through NewVolumeFlag);
        // no other SeriesUserState or VolumeUserState field is ever written here, because
        // sync does not own user state (CLAUDE.md).
        series.LastSyncedAt = timeProvider.GetUtcNow();

        return events;
    }

    /// <returns>The next main volumes the store created, which <see cref="IsNewVolumeOut"/> weighs.</returns>
    private Task<IReadOnlyList<Volume>> EnrichVolumesAsync(
        Series series,
        SyncRun run,
        DateOnly today,
        EnrichmentBudget budget,
        int expectedBonusVolumes,
        CancellationToken cancellationToken)
    {
        // No early return on an exhausted budget here: each pass checks its own allowance,
        // and one source running out must not stop the other.
        //
        // The budget is handed over rather than a number: one volume can cost several
        // requests when its own ISBN is not the one that got indexed, and only the
        // enrichment knows how many it actually spent.
        return volumeEnrichment.EnrichAsync(series, run, today, budget, expectedBonusVolumes, cancellationToken);
    }

    /// <summary>
    /// Whether this refresh brought out a volume the badge should announce, beyond the count
    /// flips ReleaseCountWriter already flags.
    /// </summary>
    // Owner decision (backlog §12, amended 2026-10-13): the store's next volumes are news,
    // unlike extras. Two ways in:
    // - a next volume the store created tonight that is already out;
    // - a volume the shelf already had that came out tonight (ShelfEventDiff's VolumeReleased),
    //   so a pre-order raises the badge the night its date passes even when the release
    //   source's count lags behind it. A count flip lands here too, which changes nothing:
    //   ReleaseCountWriter has raised it already.
    // Volumes created tonight other than next volumes (bonus volumes, whole extras, the
    // count's own new rows) are left to the rules that already govern them. Never on a first
    // refresh, which the caller checks: a series' whole backlist arriving is not news.
    private static bool IsNewVolumeOut(
        ShelfSnapshot? snapshot,
        IReadOnlyList<ShelfEvent> events,
        IReadOnlyList<Volume> nextVolumes,
        DateOnly today,
        bool releaseSourceAnswered)
    {
        if (nextVolumes.Any(volume => volume.ReleaseDate <= today))
        {
            return true;
        }

        // Only when the release source answered, because only then does LastSyncedAt move on.
        // With no answer the snapshot's day stays put, so a volume whose date passed after it
        // comes out again on every refresh: the activity log drops the repeats
        // (SeriesSyncService.AddEventsAsync), but a badge raised from them would come back
        // every night after the reader cleared it. The cost is no badge for a volume the shelf
        // had that comes out while the record is missing; its activity entry is still logged.
        if (snapshot is null || !releaseSourceAnswered)
        {
            return false;
        }

        var numbersKnownBefore = snapshot.Volumes.Values.Select(volume => volume.Number).ToHashSet();

        return events.Any(shelfEvent => shelfEvent.Kind is ShelfEventKind.VolumeReleased
                                        && numbersKnownBefore.Contains(shelfEvent.VolumeNumber));
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
