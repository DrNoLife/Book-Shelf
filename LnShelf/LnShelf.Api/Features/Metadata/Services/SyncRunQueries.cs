namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Questions asked of the user's sync runs, so the sidebar and the series page cannot
/// answer them differently.
/// </summary>
// Always called on dbContext.SyncRuns and never on the discrepancy table: SyncDiscrepancy has
// no owner filter of its own and is reached only through its run, which is filtered
// (OwnerFiltersTests).
public static class SyncRunQueries
{
    extension(IQueryable<SyncRun> runs)
    {
        /// <summary>
        /// The last finished sweep of the whole shelf, because that is the run whose items to
        /// review the sidebar exists to show. A one-series refresh or a sweep still in flight
        /// used to win on StartedAt and blank the list. Any run is the fallback, so a shelf
        /// that has never been swept still says something.
        /// </summary>
        public async Task<SyncRun?> LatestSweepAsync(CancellationToken cancellationToken)
        {
            var newestFirst = runs.OrderByDescending(candidate => candidate.StartedAt);

            return await newestFirst.FirstOrDefaultAsync(
                       candidate => candidate.CoversWholeShelf && candidate.FinishedAt != null, cancellationToken)
                   ?? await newestFirst.FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// The most recently recorded disagreement for one field of one series (and volume),
        /// from any run. Every row that run recorded for the key is read as one
        /// (<see cref="RecordedDisagreement.FromRows"/>).
        /// </summary>
        public async Task<RecordedDisagreement?> NewestDisagreementAsync(
            Guid seriesId,
            SyncDiscrepancyField field,
            decimal? volumeNumber,
            CancellationToken cancellationToken)
        {
            var run = await runs
                .AsNoTracking()
                .Where(candidate => candidate.Discrepancies.Any(discrepancy =>
                    discrepancy.SeriesId == seriesId
                    && discrepancy.Field == field
                    && discrepancy.VolumeNumber == volumeNumber))
                .OrderByDescending(candidate => candidate.StartedAt)
                .Include(candidate => candidate.Discrepancies.Where(discrepancy =>
                    discrepancy.SeriesId == seriesId
                    && discrepancy.Field == field
                    && discrepancy.VolumeNumber == volumeNumber))
                .ThenInclude(discrepancy => discrepancy.Values)
                .FirstOrDefaultAsync(cancellationToken);

            return run is null
                ? null
                : RecordedDisagreement.FromRows(run.Discrepancies, run.StartedAt);
        }
    }
}
