namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SyncQueryService(LnShelfDbContext dbContext) : ISyncQueryService
{
    // The last finished sweep of the whole shelf, because that is the run whose items to
    // review the sidebar exists to show. A one-series refresh or a sweep still in flight
    // used to win on StartedAt and blank the list. Any run is the fallback, so a shelf
    // that has never been swept still says something.
    public async Task<SyncSummaryResponse?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var runs = dbContext.SyncRuns
            .AsNoTracking()
            .Include(candidate => candidate.Discrepancies).ThenInclude(discrepancy => discrepancy.Values)
            .OrderByDescending(candidate => candidate.StartedAt);

        var run = await runs.FirstOrDefaultAsync(
                      candidate => candidate.CoversWholeShelf && candidate.FinishedAt != null, cancellationToken)
                  ?? await runs.FirstOrDefaultAsync(cancellationToken);

        return run?.ToResponse();
    }
}
