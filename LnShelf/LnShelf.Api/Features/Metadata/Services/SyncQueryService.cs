namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SyncQueryService(LnShelfDbContext dbContext) : ISyncQueryService
{
    public async Task<SyncSummaryResponse?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var run = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(candidate => candidate.Discrepancies).ThenInclude(discrepancy => discrepancy.Values)
            .OrderByDescending(candidate => candidate.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return run?.ToResponse();
    }
}
