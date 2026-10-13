namespace LnShelf.Api.Features.Recommendations.Services;

/// <summary>
/// Which suggested works the reader does not want to see: the ones already on the shelf and
/// the ones hidden with "Not interested".
/// </summary>
// No owner filter by hand: both sets are read through the context's owner filter, so they are
// this user's shelf and this user's dismissals by construction (CLAUDE.md).
public sealed class RecommendationExclusions(LnShelfDbContext dbContext)
{
    public async Task<HashSet<int>> ForAsync(IReadOnlyCollection<int> candidateIds, CancellationToken cancellationToken)
    {
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var shelved = await dbContext.Series
            .AsNoTracking()
            .Where(series => series.AniListId != null && candidateIds.Contains(series.AniListId.Value))
            .Select(series => series.AniListId!.Value)
            .ToListAsync(cancellationToken);

        var dismissed = await dbContext.RecommendationDismissals
            .AsNoTracking()
            .Where(dismissal => candidateIds.Contains(dismissal.AniListId))
            .Select(dismissal => dismissal.AniListId)
            .ToListAsync(cancellationToken);

        return [.. shelved, .. dismissed];
    }
}
