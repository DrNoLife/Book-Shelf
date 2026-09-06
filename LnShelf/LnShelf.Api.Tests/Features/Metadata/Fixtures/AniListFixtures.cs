using System.Text.Json;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Loads the captured AniList responses beside this file (testing.md #4 —
/// external payload mapping is tested against what the API actually returned,
/// never against hand-invented JSON).
/// </summary>
// Captured 2026-07-29 with the very documents AniListQueries ships, so the
// fixtures go stale the moment a query changes rather than quietly passing:
//
//   anilist-search-mushoku.json                      SeriesSearch, q=mushoku,
//                                                    all three tracked formats, perPage 20
//   anilist-media-85470-mushoku-tensei-novel.json    SeriesDetail, id 85470 (the light novel)
//
// Two amendments, flagged rather than hidden, both because a field joined a query after
// the capture:
//
//   * the media fixture's `description`. The text is not AniList's verbatim synopsis —
//     the *shape* is, and the shape is what the mapping is tested against: `<br>` pairs
//     for paragraph breaks, an <i> note, an &mdash; entity and a trailing "(Source: …)"
//     attribution, all of which arrive with asHtml: false.
//
//   * `staff` edges on two of the search fixture's twenty entries — the Mushoku Tensei
//     novel and its manga adaptation, carrying the credits AniList publishes for them
//     (the novel's pair is the same one the captured detail fixture asserts). Two rather
//     than twenty on purpose: an entry with no staff at all is what a sparse AniList row
//     looks like, and the mapping has to leave those two fields null rather than fail.
//
// Deserialization goes through AniListGraphQlClient.JsonOptions, so the tests
// exercise production's own serializer configuration too.
internal static class AniListFixtures
{
    public const string MushokuSearch = "anilist-search-mushoku.json";
    public const string MushokuTenseiNovel = "anilist-media-85470-mushoku-tensei-novel.json";

    /// <summary>AniList's media id for the Mushoku Tensei light novel.</summary>
    public const int MushokuTenseiNovelId = 85470;

    /// <summary>AniList's media id for the Mushoku Tensei manga.</summary>
    public const int MushokuTenseiMangaId = 85564;

    public static AniListSearchData LoadSearch(string fileName) => Load<AniListSearchData>(fileName);

    public static AniListMedia LoadMedia(string fileName) =>
        Load<AniListMediaData>(fileName).Media
        ?? throw new InvalidOperationException($"Fixture '{fileName}' carries no media.");

    private static TData Load<TData>(string fileName)
        where TData : class
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName);
        var json = File.ReadAllText(path);

        var envelope = JsonSerializer.Deserialize<AniListGraphQlResponse<TData>>(
            json, AniListGraphQlClient.JsonOptions);

        return envelope?.Data
               ?? throw new InvalidOperationException($"Fixture '{fileName}' carries no data.");
    }
}
