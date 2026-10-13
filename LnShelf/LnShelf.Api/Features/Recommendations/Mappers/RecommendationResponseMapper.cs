namespace LnShelf.Api.Features.Recommendations.Mappers;

public static class RecommendationResponseMapper
{
    public static RecommendationResponse ToResponse(RecommendedWork work, string? coverObjectKey, int otherSeedCount = 0) => new()
    {
        AniListId = work.ExternalId,
        TitleEnglish = work.Title,
        Format = work.Format,
        Votes = work.Votes,
        CoverObjectKey = coverObjectKey,
        OtherSeedCount = otherSeedCount,
    };
}
