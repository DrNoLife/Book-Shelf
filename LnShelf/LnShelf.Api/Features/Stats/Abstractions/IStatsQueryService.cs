namespace LnShelf.Api.Features.Stats.Abstractions;

/// <summary>
/// Reads for the Stats page. Read-only by nature: every figure is computed from the shelf.
/// </summary>
public interface IStatsQueryService
{
    Task<StatsResponse> GetStatsAsync(CancellationToken cancellationToken);
}
