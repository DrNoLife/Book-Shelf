namespace LnShelf.Api.Features.Recommendations.Abstractions;

/// <summary>The "Readers also liked" strip of one series.</summary>
public interface ISeriesRecommendationsQueryService
{
    /// <returns>Null when the shelf has no such series.</returns>
    Task<SeriesRecommendationsResponse?> GetForSeriesAsync(Guid seriesId, CancellationToken cancellationToken);
}
