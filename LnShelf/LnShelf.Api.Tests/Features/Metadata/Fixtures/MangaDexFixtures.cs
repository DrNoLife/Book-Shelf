using System.Text.Json;
using LnShelf.Api.Features.Metadata.Models.MangaDex;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Captured real MangaDex responses (testing.md #4).
/// </summary>
// Captured 2026-10-14 with the very requests MangaDexClient sends:
//
//   mangadex-search-<series>.json
//       GET /manga?title=<title>&limit=10&contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica
//       Every record kept, each trimmed to its title, originalLanguage and links.
//       Slime's top hit is the Tensura Nikki spin-off; the series itself is second, with a
//       zero-padded MangaUpdates slug ("04gtb06"). Solo Leveling's records are all Korean.
//   mangadex-aggregate-<series>-en.json
//       GET /manga/{id}/aggregate?translatedLanguage[]=en&includeUnavailable=1
//   mangadex-aggregate-<series>-all.json
//       GET /manga/{id}/aggregate?includeUnavailable=1
//       Trimmed of every chapter's `id` and `others` lists, nothing else: the keys, the "none"
//       and "0" volumes, Slime's array-valued vol 0 chapters, and the strays (Chainsaw Man
//       ch 232 under vol 1 and ch 203 under vol 20, Slime ch 138 under vol 4) are verbatim.
//   mangadex-aggregate-86-empty.json
//       86's all-languages aggregate without includeUnavailable: `"volumes": []`, an empty
//       array where an object is expected.
internal static class MangaDexFixtures
{
    public const string ChainsawManSearch = "mangadex-search-chainsaw-man.json";
    public const string ChainsawManEnglish = "mangadex-aggregate-chainsaw-man-en.json";
    public const string ChainsawManAll = "mangadex-aggregate-chainsaw-man-all.json";
    public const int ChainsawManAniListId = 105778;
    public static readonly Guid ChainsawManId = Guid.Parse("a77742b1-befd-49a4-bff5-1ad4e6b0ef7b");

    public const string FrierenSearch = "mangadex-search-frieren.json";
    public const string FrierenEnglish = "mangadex-aggregate-frieren-en.json";
    public const string FrierenAll = "mangadex-aggregate-frieren-all.json";

    public const string SlimeSearch = "mangadex-search-slime.json";
    public const string SlimeEnglish = "mangadex-aggregate-slime-en.json";
    public const string SlimeAll = "mangadex-aggregate-slime-all.json";
    public const int SlimeAniListId = 86399;

    /// <summary>MangaUpdates' "04gtb06": seven characters, zero-padded.</summary>
    public const long SlimeMangaUpdatesId = 270105846;

    public static readonly Guid SlimeId = Guid.Parse("e78a489b-6632-4d61-b00b-5206f5b8b22b");

    public const string SoloLevelingSearch = "mangadex-search-solo-leveling.json";
    public const string SoloLevelingEnglish = "mangadex-aggregate-solo-leveling-en.json";
    public const string SoloLevelingAll = "mangadex-aggregate-solo-leveling-all.json";

    public const string EmptyAggregate = "mangadex-aggregate-86-empty.json";

    public static MangaDexMangaListResponse LoadSearch(string fileName) => Load<MangaDexMangaListResponse>(fileName);

    public static MangaDexAggregateResponse LoadAggregate(string fileName) => Load<MangaDexAggregateResponse>(fileName);

    /// <summary>The source-neutral lists the builder reads, from a pair of captured aggregates.</summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<decimal>> ChaptersByVolume(string english, string all) =>
        MangaDexAggregateMapper.ToChaptersByVolume(LoadAggregate(english), LoadAggregate(all));

    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName));

    private static TResponse Load<TResponse>(string fileName) =>
        JsonSerializer.Deserialize<TResponse>(Read(fileName), MangaDexClient.JsonOptions)
        ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
}
