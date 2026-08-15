using System.Text.Json;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Captured real MangaUpdates responses (testing.md #4).
/// </summary>
// Captured with the very requests MangaUpdatesClient sends.
//
// 2026-07-29, for linking:
//
//   mangaupdates-search-youjo-senki.json                POST /v1/series/search,
//                                                       {"search":"Youjo Senki","type":["Novel"]}
//   mangaupdates-series-24348008396-youjo-senki.json    GET /v1/series/24348008396
//
// Youjo Senki is the useful specimen: MangaUpdates files it under its romaji title
// with the shelf's English name only in `associated`, which is the case that makes
// or breaks linking.
//
// 2026-08-15, for search-and-add — captured on the day AniList disabled its public API,
// against the live MangaUpdates that answered normally throughout:
//
//   mangaupdates-search-torture-princess.json                    POST /v1/series/search,
//                                                                {"search":"torture princess",
//                                                                 "type":["Novel"],"perpage":5}
//   mangaupdates-series-17992364412-fremd-torturchen.json        GET /v1/series/17992364412
//
// Fremd Torturchen is the specimen for adding: a romaji primary title with a " (Novel)"
// marker, a completed status carrying an extra volume ("9 Volumes + 1 Extra (Complete)"),
// both an Author and an Artist, and two publishers of which only one is the English one.
// The search response is the same pair's other half, and it is worth having precisely for
// what it does *not* contain — no status, no completed flag, no authors.
internal static class MangaUpdatesFixtures
{
    public const string YoujoSenkiSearch = "mangaupdates-search-youjo-senki.json";
    public const string YoujoSenkiSeries = "mangaupdates-series-24348008396-youjo-senki.json";

    public const long YoujoSenkiSeriesId = 24348008396;

    public const string TorturePrincessSearch = "mangaupdates-search-torture-princess.json";

    public const string FremdTorturchenSeries =
        "mangaupdates-series-17992364412-fremd-torturchen.json";

    public const long FremdTorturchenSeriesId = 17992364412;

    public static MangaUpdatesSearchResponse LoadSearch(string fileName) =>
        Load<MangaUpdatesSearchResponse>(fileName);

    public static MangaUpdatesSeriesRecord LoadSeries(string fileName) =>
        Load<MangaUpdatesSeriesRecord>(fileName);

    private static TData Load<TData>(string fileName)
        where TData : class
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName);
        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<TData>(json, MangaUpdatesClient.JsonOptions)
               ?? throw new InvalidOperationException($"Fixture '{fileName}' deserialized to null.");
    }
}
