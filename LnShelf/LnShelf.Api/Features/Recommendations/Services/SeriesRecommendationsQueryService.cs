namespace LnShelf.Api.Features.Recommendations.Services;

/// <inheritdoc />
public sealed class SeriesRecommendationsQueryService(
    LnShelfDbContext dbContext,
    IRecommendationSource recommendationSource,
    RecommendationExclusions exclusions,
    ISeriesCoverCache coverCache) : ISeriesRecommendationsQueryService
{
    private const int StripLimit = 12;

    public async Task<SeriesRecommendationsResponse?> GetForSeriesAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        // No owner filter by hand: the context's filter leaves this user's series.
        var series = await dbContext.Series
            .AsNoTracking()
            .Where(candidate => candidate.Id == seriesId)
            .Select(candidate => new { candidate.AniListId })
            .SingleOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return null;
        }

        if (series.AniListId is not { } aniListId)
        {
            return new SeriesRecommendationsResponse();
        }

        var answers = await recommendationSource.GetForSeriesAsync([aniListId], cancellationToken);
        if (!answers.TryGetValue(aniListId, out var answer))
        {
            return new SeriesRecommendationsResponse();
        }

        var excluded = await exclusions.ForAsync([.. answer.Works.Select(work => work.ExternalId)], cancellationToken);
        var shown = answer.Works
            .Where(work => !excluded.Contains(work.ExternalId))
            .OrderByDescending(work => work.Votes)
            .ThenBy(work => work.ExternalId)
            .Take(StripLimit)
            .ToList();

        // Sequential: each is at most one image fetch, and the strip is small.
        var works = new List<RecommendationResponse>(shown.Count);
        foreach (var work in shown)
        {
            var coverKey = await coverCache.EnsureCachedAsync(work.Source, work.ExternalId, work.CoverImageUrl, cancellationToken);
            works.Add(RecommendationResponseMapper.ToResponse(work, coverKey));
        }

        return new SeriesRecommendationsResponse { Works = works };
    }
}
