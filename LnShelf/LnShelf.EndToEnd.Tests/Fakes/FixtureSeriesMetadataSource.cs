using System.Text.Json;

namespace LnShelf.EndToEnd.Tests.Fakes;

/// <summary>
/// AniList, answered from the captured payloads the unit tests already map
/// (testing.md #4), so no journey ever reaches the real API.
/// </summary>
// Only Mushoku Tensei is known, because that is what was captured: a search whose query
// mentions it gets the captured page, and the light novel's id gets the captured detail.
// Everything else finds nothing, which is also what an import of an unknown title sees.
// The fake is stateless on purpose: HybridCache keeps search results across tests.
public sealed class FixtureSeriesMetadataSource : ISeriesMetadataSource
{
    public const string KnownQuery = "mushoku";
    public const int MushokuTenseiNovelId = 85470;

    // The captured detail query did not ask for coverImage, so the light novel gets a cover
    // URL here, without touching the payload the unit tests map. Adding it then caches a series
    // cover through the real CoverFetcher, which StubImageHandler answers, so a journey can
    // reach that art the way a visitor would (the shared-shelf cover checks rely on it).
    private const string MushokuTenseiNovelCoverUrl =
        "https://s4.anilist.co/file/anilistcdn/media/manga/cover/large/bx85470.jpg";

    private static readonly Lazy<IReadOnlyList<SeriesSearchResult>> SearchResults = new(LoadSearchResults);
    private static readonly Lazy<SeriesMetadata> MushokuTenseiNovel = new(LoadMushokuTenseiNovel);

    public Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        if (!query.Contains(KnownQuery, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<SeriesSearchResult>>([]);
        }

        IReadOnlyList<SeriesSearchResult> results = format is null
            ? SearchResults.Value
            : [.. SearchResults.Value.Where(result => result.Format == format)];

        return Task.FromResult(results);
    }

    public Task<SeriesMetadata?> GetSeriesAsync(int externalId, CancellationToken cancellationToken) =>
        Task.FromResult(externalId == MushokuTenseiNovelId ? MushokuTenseiNovel.Value : null);

    private static IReadOnlyList<SeriesSearchResult> LoadSearchResults()
    {
        var media = Load<AniListSearchData>("anilist-search-mushoku.json").Page?.Media ?? [];

        return [.. media.Select(AniListMediaMapper.ToSearchResult).OfType<SeriesSearchResult>()];
    }

    private static SeriesMetadata LoadMushokuTenseiNovel()
    {
        var media = Load<AniListMediaData>("anilist-media-85470-mushoku-tensei-novel.json").Media
                    ?? throw new InvalidOperationException("The Mushoku Tensei fixture carries no media.");

        return AniListMediaMapper.ToSeriesMetadata(media) with { CoverImageUrl = MushokuTenseiNovelCoverUrl };
    }

    // Through AniListGraphQlClient.JsonOptions, so the fixtures are read exactly as the
    // production client reads a live response.
    private static TData Load<TData>(string fileName)
        where TData : class
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        var envelope = JsonSerializer.Deserialize<AniListGraphQlResponse<TData>>(json, AniListGraphQlClient.JsonOptions);

        return envelope?.Data ?? throw new InvalidOperationException($"Fixture '{fileName}' carries no data.");
    }
}
