namespace LnShelf.Api.Features.Recommendations.Abstractions;

/// <summary>Reads the works hidden with "Not interested".</summary>
public interface IRecommendationDismissalQueryService
{
    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<RecommendationDismissalResponse>> ListAsync(CancellationToken cancellationToken);
}
