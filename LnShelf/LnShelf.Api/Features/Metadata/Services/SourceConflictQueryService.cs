namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SourceConflictQueryService(LnShelfDbContext dbContext) : ISourceConflictQueryService
{
    public async Task<IReadOnlyList<SourceConflictResponse>?> GetAsync(
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .AsNoTracking()
            .Include(candidate => candidate.SourceChoices)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        // Every disagreement the last sweep recorded for this series, settled or not. The
        // settled ones are the only way to reach some of them: MangaUpdates counting fewer
        // volumes is always resolved automatically, so a volume-count choice could never be
        // made from a list of only the open ones. Through SyncRuns, which is owner-filtered;
        // the discrepancy table has no filter of its own.
        var sweep = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(run => run.Discrepancies.Where(discrepancy => discrepancy.SeriesId == seriesId))
            .ThenInclude(discrepancy => discrepancy.Values)
            .LatestSweepAsync(cancellationToken);

        var recorded = sweep is null
            ? []
            : sweep.Discrepancies
                .GroupBy(discrepancy => (discrepancy.Field, discrepancy.VolumeNumber))
                .Select(group => RecordedDisagreement.FromRows(group, sweep.StartedAt))
                .ToList();

        // Chapter ranges are checked weekly, not nightly (ChapterMapRefresher.IsDue), so the
        // latest sweep usually never looked: what the last check found still stands, and is read
        // from the run that made that check.
        if (!recorded.Any(row => row.Discrepancy.Field is SyncDiscrepancyField.ChapterRange)
            && series.ChapterMapCheckedAt is { } checkedAt)
        {
            recorded.AddRange(await LastChapterMapCheckAsync(seriesId, checkedAt, cancellationToken));
        }

        // A choice outlives the sweep that showed its disagreement: when the sources agree
        // again nothing is recorded, but the choice stands and its page entry must too.
        foreach (var choice in series.SourceChoices)
        {
            if (recorded.Any(row => choice.Covers(row.Discrepancy)))
            {
                continue;
            }

            var newest = await dbContext.SyncRuns.NewestDisagreementAsync(
                seriesId, choice.Field, choice.VolumeNumber, cancellationToken);

            if (newest is not null)
            {
                recorded.Add(newest);
            }
        }

        return
        [
            .. recorded
                .OrderBy(row => OrderOf(row.Discrepancy.Field))
                .ThenBy(row => row.Discrepancy.VolumeNumber)
                .Select(row => SourceConflictMapper.ToConflict(
                    row, series.SourceChoices.FirstOrDefault(choice => choice.Covers(row.Discrepancy)))),
        ];
    }

    /// <summary>
    /// The chapter-range disagreements of the run during which the series' chapter map was last
    /// checked; none when that check found none.
    /// </summary>
    private async Task<IEnumerable<RecordedDisagreement>> LastChapterMapCheckAsync(
        Guid seriesId,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        // Through SyncRuns, which is owner-filtered, like the sweep above.
        var run = await dbContext.SyncRuns
            .AsNoTracking()
            .Where(candidate => candidate.StartedAt <= checkedAt
                                && (candidate.FinishedAt == null || candidate.FinishedAt >= checkedAt))
            .OrderByDescending(candidate => candidate.StartedAt)
            .Include(candidate => candidate.Discrepancies.Where(discrepancy =>
                discrepancy.SeriesId == seriesId && discrepancy.Field == SyncDiscrepancyField.ChapterRange))
            .ThenInclude(discrepancy => discrepancy.Values)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null
            ? []
            : run.Discrepancies
                .GroupBy(discrepancy => discrepancy.VolumeNumber)
                .Select(group => RecordedDisagreement.FromRows(group, run.StartedAt));
    }

    // Status, then the count, then volumes in order; the link question last because it is
    // about the series as a whole and has nothing to choose.
    private static int OrderOf(SyncDiscrepancyField field) => field switch
    {
        SyncDiscrepancyField.Status => 0,
        SyncDiscrepancyField.VolumeCount => 1,
        SyncDiscrepancyField.ReleaseDate => 2,
        _ => 3,
    };
}
