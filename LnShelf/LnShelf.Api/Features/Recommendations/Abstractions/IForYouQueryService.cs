namespace LnShelf.Api.Features.Recommendations.Abstractions;

/// <summary>The For you page: suggestions drawn from the shelf's own series.</summary>
public interface IForYouQueryService
{
    Task<ForYouResponse> GetAsync(CancellationToken cancellationToken);
}
