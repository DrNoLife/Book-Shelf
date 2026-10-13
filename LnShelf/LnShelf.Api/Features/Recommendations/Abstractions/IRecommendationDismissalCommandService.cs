namespace LnShelf.Api.Features.Recommendations.Abstractions;

/// <summary>Hides a work from For you, and shows it again.</summary>
public interface IRecommendationDismissalCommandService
{
    /// <summary>Idempotent: hiding a work that is already hidden changes nothing.</summary>
    Task DismissAsync(int aniListId, DismissRecommendationRequest request, CancellationToken cancellationToken);

    /// <returns>False when the work was not hidden.</returns>
    Task<bool> RestoreAsync(int aniListId, CancellationToken cancellationToken);
}
