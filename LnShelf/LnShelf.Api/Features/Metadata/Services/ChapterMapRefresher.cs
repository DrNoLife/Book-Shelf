namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The chapter-map step of one series' refresh: asks every chapter-map source in precedence
/// order, judges each answer, and writes the ranges onto the series' volumes.
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
        // cost this series its count, dates and covers. Nothing has been written when this
        // lands — answers are all gathered before the writer runs — so the old ranges stand.
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

        foreach (var source in sources)
        {
            var answer = await source.GetChapterMapAsync(series, cancellationToken);

            switch (answer.Kind)
            {
                // Left whole for the next run: judging a series on some of its sources would
                // settle a status the missing one might change.
                case ChapterMapAnswerKind.Skipped:
                    logger.LogInformation(
                        "Chapter map for {TitleEnglish} left for the next run: {Source}'s allowance is spent",
                        series.TitleEnglish,
                        source.SourceName);
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

        run.ChapterRangesWritten += ChapterRangeWriter.Apply(series, maps);

        series.ChapterMapStatus = (maps.Count, refusals.Count) switch
        {
            ( > 0, _) => ChapterMapStatus.Mapped,
            (_, > 0) => ChapterMapStatus.Rejected,
            _ => ChapterMapStatus.Unmatched,
        };
        series.ChapterMapCheckedAt = now;

        logger.LogInformation(
            "Chapter map for {TitleEnglish}: {ChapterMapStatus}, {RangedVolumeCount} volumes ranged; refused: {Refusals}",
            series.TitleEnglish,
            series.ChapterMapStatus,
            series.Volumes.Count(volume => volume.FirstChapter is not null),
            refusals.Count > 0 ? string.Join("; ", refusals) : "none");
    }

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
        IsCompleted: series.PublicationStatus is PublicationStatus.Completed);

    // Out by the release source's word or a date that has passed — the shelf's own ground for
    // "released", minus ownership, which says nothing about whether a map should cover it.
    private static IEnumerable<Volume> ReleasedWholeVolumes(Series series, DateOnly today) =>
        series.Volumes.Where(volume => IsWholeVolume(volume) && (volume.IsReleased || volume.ReleaseDate <= today));

    private static bool IsWholeVolume(Volume volume) =>
        volume.Number >= 1 && volume.Number == decimal.Truncate(volume.Number);
}
