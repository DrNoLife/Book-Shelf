using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Fakes;

/// <summary>
/// AniList's recommendations, answered from the captured payload the unit tests already map
/// (testing.md #4). No journey ever reaches AniList.
/// </summary>
// Stateless on purpose, like FixtureSeriesMetadataSource: HybridCache keeps answers across tests.
// Only the captured series (the Mushoku Tensei novel and manga) have suggestions; every other
// id gets an empty answer, which is what the real source says for a series it knows nothing about.
public sealed class FixtureRecommendationSource : IRecommendationSource
{
    private const string FixtureFileName = "anilist-recommendations-85470-85564-mushoku-tensei.json";

    private static readonly Lazy<IReadOnlyDictionary<int, SeriesRecommendations>> Known = new(Load);

    /// <summary>What the fixture holds for a series, so a journey can read the expected titles instead of hard-coding them.</summary>
    public static SeriesRecommendations For(int externalId) =>
        Known.Value.TryGetValue(externalId, out var recommendations)
            ? recommendations
            : SeriesRecommendations.None(externalId);

    public Task<IReadOnlyDictionary<int, SeriesRecommendations>> GetForSeriesAsync(
        IReadOnlyCollection<int> externalIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, SeriesRecommendations> answer =
            externalIds.Distinct().ToDictionary(externalId => externalId, For);

        return Task.FromResult(answer);
    }

    private static IReadOnlyDictionary<int, SeriesRecommendations> Load()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureFileName));
        var envelope = JsonSerializer.Deserialize<AniListGraphQlResponse<AniListSearchData>>(json, AniListGraphQlClient.JsonOptions);
        var media = envelope?.Data?.Page?.Media
                    ?? throw new InvalidOperationException($"Fixture '{FixtureFileName}' carries no media.");

        return media
            .Select(AniListRecommendationsMapper.ToRecommendations)
            .ToDictionary(recommendations => recommendations.ExternalId);
    }
}
