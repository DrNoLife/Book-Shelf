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

        // Chapter ranges are left out of the sweep's rows and read from the run that last
        // checked the map instead (below), so they are never listed twice.
        var recorded = sweep is null
            ? []
            : sweep.Discrepancies
                .Where(discrepancy => discrepancy.Field is not SyncDiscrepancyField.ChapterRange)
                .GroupBy(discrepancy => (discrepancy.Field, discrepancy.VolumeNumber))
                .Select(group => RecordedDisagreement.FromRows(group, sweep.StartedAt))
                .ToList();

        // Chapter ranges are checked weekly, not nightly (ChapterMapRefresher.IsDue), so the
        // latest sweep usually never looked: what the last check found still stands, and is read
        // from the run that made that check.
        if (series.ChapterMapSyncRunId is { } checkingRunId)
        {
            recorded.AddRange(await ChapterMapCheckAsync(seriesId, checkingRunId, cancellationToken));
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
    /// The chapter-range disagreements the series' last chapter-map check recorded; none when
    /// it found none.
    /// </summary>
    private async Task<IEnumerable<RecordedDisagreement>> ChapterMapCheckAsync(
        Guid seriesId,
        Guid checkingRunId,
        CancellationToken cancellationToken)
    {
        // Through SyncRuns, which is owner-filtered, like the sweep above.
        var run = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(candidate => candidate.Discrepancies.Where(discrepancy =>
                discrepancy.SeriesId == seriesId && discrepancy.Field == SyncDiscrepancyField.ChapterRange))
            .ThenInclude(discrepancy => discrepancy.Values)
            .FirstOrDefaultAsync(candidate => candidate.Id == checkingRunId, cancellationToken);

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
