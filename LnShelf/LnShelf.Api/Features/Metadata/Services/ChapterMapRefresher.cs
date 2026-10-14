namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The chapter-map step of one series' refresh: asks every chapter-map source in precedence
/// order, judges each answer, merges the usable ones (ChapterMapMerger) and writes the ranges
/// onto the series' volumes.
/// </summary>
// A collaborator of SeriesRefresher rather than more of it, so the refresher stays the short
// sequence of steps it was cut down to. Called on every refresh, nightly and on demand alike
// (feature-spec §4.2 — "same code path"); the cadence below is what keeps most series from
// costing a request on most nights.
public sealed class ChapterMapRefresher(
    IEnumerable<IChapterMapSource> sources,
    TimeProvider timeProvider,
    ILogger<ChapterMapRefresher> logger)
{
    /// <summary>A map does not change between one night and the next; a week is soon enough to see a new volume's start.</summary>
    private static readonly TimeSpan RecheckAfter = TimeSpan.FromDays(7);

    /// <summary>A series no source can map is asked about once a month, not every week.</summary>
    private static readonly TimeSpan RecheckUnusableAfter = TimeSpan.FromDays(30);

    public async Task RefreshAsync(Series series, SyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);

        var now = timeProvider.GetUtcNow();
        var today = timeProvider.Today();

        if (!IsDue(series, now, today))
        {
            return;
        }

        // Every allowance before any request: once one source's budget for the run is spent,
        // asking the others would spend theirs on a series that is left for the next run anyway.
        if (sources.FirstOrDefault(source => !source.CanAnswer(series)) is { } shortSource)
        {
            LogLeftForNextRun(series, shortSource);
            return;
        }

        try
        {
            await MapAsync(series, run, now, today, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Isolated within the series as well as across them (CLAUDE.md — the sync job's
        // exception): chapter ranges are a nice-to-have, and a flaky community index must not
        // cost this series its count, dates and covers. Each source's own failure is already
        // isolated in AskAsync; this is what is left (the builder, the merge, the writer).
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Chapter map failed for {TitleEnglish} (AniList {AniListId}); its chapter ranges stay as they were",
                series.TitleEnglish,
                series.AniListId);
        }
    }

    private async Task MapAsync(Series series, SyncRun run, DateTimeOffset now, DateOnly today, CancellationToken cancellationToken)
    {
        var shelf = ShelfOf(series, today);
        var maps = new List<(MetadataSourceName Source, ChapterMap Map)>();
        var refusals = new List<string>();
        var failures = 0;

        foreach (var source in sources)
        {
            var answer = await AskAsync(source, series, cancellationToken);

            switch (answer?.Kind)
            {
                case null:
                    failures++;
                    break;

                // Left whole for the next run: judging a series on some of its sources would
                // settle a status the missing one might change. CanAnswer makes this rare.
                case ChapterMapAnswerKind.Skipped:
                    LogLeftForNextRun(series, source);
                    return;

                case ChapterMapAnswerKind.Found:
                    var map = ChapterMapBuilder.Build(answer.ChaptersByVolume, shelf);
                    if (map.IsUsable)
                    {
                        maps.Add((source.SourceName, map));
                    }
                    else
                    {
                        refusals.Add($"{source.SourceName.DisplayName}: {map.RejectedBecause}");
                    }

                    break;

                case ChapterMapAnswerKind.Rejected:
                    refusals.Add($"{source.SourceName.DisplayName}: {answer.Reason}");
                    break;

                case ChapterMapAnswerKind.Unmatched:
                    break;
            }
        }

        // Stamped either way, so a source that fails every time (a dead link, a page that no
        // longer parses) is asked again on the cadence rather than every night.
        series.ChapterMapCheckedAt = now;

        // A failed source is unanswered, not "no map": with nothing usable from the others, the
        // ranges and status from the last good check stand rather than being cleared over one
        // flaky request.
        if (failures > 0 && maps.Count == 0)
        {
            logger.LogInformation(
                "Chapter map for {TitleEnglish}: {FailedSourceCount} source(s) failed and no other mapped it; its ranges stay as they were",
                series.TitleEnglish,
                failures);
            return;
        }

        Write(series, run, maps, refusals);
    }

    private void Write(
        Series series,
        SyncRun run,
        List<(MetadataSourceName Source, ChapterMap Map)> maps,
        List<string> refusals)
    {
        var merged = ChapterMapMerger.Merge(maps);
        run.ChapterRangesWritten += ChapterRangeWriter.Apply(series, merged.Ranges);

        // Only for volumes the shelf tracks: a disagreement about a volume nobody can see is noise.
        foreach (var disagreement in merged.Disagreements.Where(disagreement =>
                     series.Volumes.Any(volume => volume.Number == disagreement.VolumeNumber)))
        {
            run.Discrepancies.Add(SyncDiscrepancies.ChapterRange(series, disagreement));
        }

        series.ChapterMapStatus = (maps.Count, refusals.Count) switch
        {
            ( > 0, _) => ChapterMapStatus.Mapped,
            (_, > 0) => ChapterMapStatus.Rejected,
            _ => ChapterMapStatus.Unmatched,
        };
        series.ChapterMapSyncRunId = run.Id;

        logger.LogInformation(
            "Chapter map for {TitleEnglish}: {ChapterMapStatus}, {RangedVolumeCount} volumes ranged; refused: {Refusals}",
            series.TitleEnglish,
            series.ChapterMapStatus,
            series.Volumes.Count(volume => volume.FirstChapter is not null),
            refusals.Count > 0 ? string.Join("; ", refusals) : "none");
    }

    /// <returns>The source's answer, or null when asking it threw.</returns>
    // One source's failure is that source's, not the series': the others' answers still count
    // (CLAUDE.md — the sync job isolates failures, and a chapter map is a nice-to-have).
    private async Task<ChapterMapAnswer?> AskAsync(IChapterMapSource source, Series series, CancellationToken cancellationToken)
    {
        try
        {
            return await source.GetChapterMapAsync(series, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "{Source} failed to answer the chapter map for {TitleEnglish} (AniList {AniListId}); counted as unanswered",
                source.SourceName,
                series.TitleEnglish,
                series.AniListId);

            return null;
        }
    }

    private void LogLeftForNextRun(Series series, IChapterMapSource source) =>
        logger.LogInformation(
            "Chapter map for {TitleEnglish} left for the next run: {Source}'s allowance is spent",
            series.TitleEnglish,
            source.SourceName);

    /// <summary>
    /// Manga only, linked to a source, and not checked too recently.
    /// </summary>
    // Manga only: a light novel's chapters are not what its readers count, and a web novel has
    // no volumes worth mapping. A mapped, completed series whose released volumes all have both
    // ends is finished business, and is not asked about again.
    public static bool IsDue(Series series, DateTimeOffset now, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Format is not Format.Manga || (series.AniListId is null && series.MangaUpdatesId is null))
        {
            return false;
        }

        if (series.ChapterMapCheckedAt is not { } checkedAt)
        {
            return true;
        }

        var age = now - checkedAt;

        if (age < RecheckAfter)
        {
            return false;
        }

        if (series.ChapterMapStatus is ChapterMapStatus.Unmatched or ChapterMapStatus.Rejected)
        {
            return age >= RecheckUnusableAfter;
        }

        var settled = series.ChapterMapStatus is ChapterMapStatus.Mapped
                      && series.PublicationStatus is PublicationStatus.Completed
                      && ReleasedWholeVolumes(series, today)
                          .All(volume => volume.FirstChapter is not null && volume.LastChapter is not null);

        return !settled;
    }

    private static ChapterMapShelf ShelfOf(Series series, DateOnly today) => new(
        TrackedWholeVolumes: series.Volumes.Count(IsWholeVolume),
        ReleasedWholeVolumes: ReleasedWholeVolumes(series, today).Select(volume => (int)volume.Number).ToHashSet(),
        LatestChapter: series.LatestChapter,
        IsCompleted: series.PublicationStatus is PublicationStatus.Completed,
        HighestTrackedWholeVolume: series.Volumes.Where(IsWholeVolume).Select(volume => (int?)(int)volume.Number).Max());

    // Out by the release source's word or a date that has passed — the shelf's own ground for
    // "released", minus ownership, which says nothing about whether a map should cover it.
    private static IEnumerable<Volume> ReleasedWholeVolumes(Series series, DateOnly today) =>
        series.Volumes.Where(volume => IsWholeVolume(volume) && (volume.IsReleased || volume.ReleaseDate <= today));

    private static bool IsWholeVolume(Volume volume) =>
        volume.Number >= 1 && !volume.Number.IsBonusVolume;
}
