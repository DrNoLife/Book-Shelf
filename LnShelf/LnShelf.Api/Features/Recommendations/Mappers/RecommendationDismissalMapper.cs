namespace LnShelf.Api.Features.Recommendations.Mappers;

/// <summary>Hand-written, explicit, no reflection (CLAUDE.md, Mapping).</summary>
public static class RecommendationDismissalMapper
{
    public static RecommendationDismissalResponse ToResponse(RecommendationDismissal dismissal) => new()
    {
        AniListId = dismissal.AniListId,
        Title = dismissal.Title,
        Format = dismissal.Format,
        DismissedAt = dismissal.DismissedAt,
    };
}
