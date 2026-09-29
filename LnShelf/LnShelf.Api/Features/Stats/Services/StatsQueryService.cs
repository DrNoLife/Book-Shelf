namespace LnShelf.Api.Features.Stats.Services;

/// <inheritdoc />
public sealed class StatsQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : IStatsQueryService
{
    public async Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken)
    {
        // The whole shelf in one query. A single user's few hundred series is small enough
        // that shaping it in memory beats a dozen aggregate queries — the same call
        // LibraryQueryService makes about its totals.
        var series = await dbContext.Series
            .AsNoTracking()
            .Include(entry => entry.UserState)
            .Include(entry => entry.Volumes).ThenInclude(volume => volume.UserState)
            .ToListAsync(cancellationToken);

        return StatsCalculator.Calculate(series, timeProvider.Today());
    }
}
