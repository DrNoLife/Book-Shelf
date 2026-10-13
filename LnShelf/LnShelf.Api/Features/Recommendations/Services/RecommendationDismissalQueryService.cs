namespace LnShelf.Api.Features.Recommendations.Services;

/// <inheritdoc />
// No owner filter by hand: the context's filter leaves this user's rows (CLAUDE.md).
public sealed class RecommendationDismissalQueryService(LnShelfDbContext dbContext) : IRecommendationDismissalQueryService
{
    public async Task<IReadOnlyList<RecommendationDismissalResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var dismissals = await dbContext.RecommendationDismissals
            .AsNoTracking()
            .OrderByDescending(dismissal => dismissal.DismissedAt)
            .ThenBy(dismissal => dismissal.AniListId)
            .ToListAsync(cancellationToken);

        return dismissals.Select(RecommendationDismissalMapper.ToResponse).ToList();
    }
}
