namespace LnShelf.Api.Features.Recommendations.Services;

/// <inheritdoc />
public sealed class ForYouQueryService(
    LnShelfDbContext dbContext,
    IRecommendationSource recommendationSource,
    RecommendationExclusions exclusions,
    ISeriesCoverCache coverCache,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<ForYouQueryService> logger) : IForYouQueryService
{
    public async Task<ForYouResponse> GetAsync(CancellationToken cancellationToken)
    {
        // One projection for the whole shelf, no per-series round trips (data-access.md).
        // The context's owner filter leaves this user's series.
        var facts = await dbContext.Series
            .AsNoTracking()
            .Where(series => series.AniListId != null)
            .Select(series => new ShelfSeedFacts(
                series.Id,
                series.AniListId,
                series.TitleEnglish,
                series.Format,
                series.UserState == null ? TrackingStatus.PlanToStart : series.UserState.TrackingStatus,
                series.Volumes.Count(volume => volume.UserState != null && volume.UserState.ReadStatus == ReadStatus.Read),
                series.UserState != null && series.UserState.LastChapterRead > 0,
                series.UserState == null ? null : series.UserState.LastReadAt,
                series.Volumes.Max(volume => volume.UserState == null ? null : volume.UserState.ReadAt),
                series.UserState == null ? null : series.UserState.StartedAt))
            .ToListAsync(cancellationToken);

        var seeds = SeedWeights.Choose(facts, timeProvider.Today());
        var positiveSeedCount = seeds.Count(seed => seed.Weight > 0);
        if (positiveSeedCount == 0)
        {
            return new ForYouResponse { SeedCount = 0 };
        }

        var recommendations = await recommendationSource.GetForSeriesAsync(
            [.. seeds.Select(seed => seed.ExternalId)], cancellationToken);
        var candidateIds = recommendations.Values
            .SelectMany(entry => entry.Works)
            .Select(work => work.ExternalId)
            .Distinct()
            .ToList();
        var excluded = await exclusions.ForAsync(candidateIds, cancellationToken);

        var rows = ForYouRanker.Rank(seeds, recommendations, excluded);

        // Covers only for what is shown (at most 6 x 8), one at a time.
        var rowResponses = new List<ForYouRowResponse>(rows.Count);
        foreach (var row in rows)
        {
            var works = new List<RecommendationResponse>(row.Picks.Count);
            foreach (var pick in row.Picks)
            {
                var coverKey = await coverCache.EnsureCachedAsync(
                    pick.Work.Source, pick.Work.ExternalId, pick.Work.CoverImageUrl, cancellationToken);
                works.Add(RecommendationResponseMapper.ToResponse(pick.Work, coverKey, pick.OtherSeedCount));
            }

            rowResponses.Add(new ForYouRowResponse
            {
                BecauseOfSeriesId = row.Seed.SeriesId,
                BecauseOfTitle = row.Seed.Title,
                Format = row.Seed.Format,
                Works = works,
            });
        }

        logger.LogInformation(
            "For you for {UserId}: {SeedCount} seeds, {RowCount} rows, {PickCount} picks",
            currentUser.IdOrNull, positiveSeedCount, rowResponses.Count, rowResponses.Sum(row => row.Works.Count));

        return new ForYouResponse { SeedCount = positiveSeedCount, Rows = rowResponses };
    }
}
