namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SyncQueryService(LnShelfDbContext dbContext) : ISyncQueryService
{
    public async Task<SyncSummaryResponse?> GetLatestAsync(CancellationToken cancellationToken)
    {
        var run = await dbContext.SyncRuns
            .AsNoTracking()
            .Include(candidate => candidate.Discrepancies).ThenInclude(discrepancy => discrepancy.Values)
            .LatestSweepAsync(cancellationToken);

        if (run is null)
        {
            return null;
        }

        return await DescribeAsync(run, cancellationToken);
    }

    // Whether a disagreement is still waiting on the user is worked out here from the choices
    // that stand now, not read from the row: the row says what the run did, and a choice made
    // (or reset) since then has to show at once without rewriting a log. Owner-filtered
    // through Series, like the runs.
    public async Task<SyncSummaryResponse> DescribeAsync(SyncRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        var choices = await dbContext.SourceChoices.AsNoTracking().ToListAsync(cancellationToken);

        return run.ToResponse(choices);
    }
}
