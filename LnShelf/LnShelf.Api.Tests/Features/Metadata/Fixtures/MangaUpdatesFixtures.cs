using System.Text.Json;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Captured real MangaUpdates responses (testing.md #4).
/// </summary>
// Captured 2026-07-29 with the very requests MangaUpdatesClient sends:
//
//   mangaupdates-search-youjo-senki.json                POST /v1/series/search,
//                                                       {"search":"Youjo Senki","type":["Novel"]}
//   mangaupdates-series-24348008396-youjo-senki.json    GET /v1/series/24348008396
//
// Youjo Senki is the useful specimen: MangaUpdates files it under its romaji title
// with the shelf's English name only in `associated`, which is the case that makes
// or breaks linking.
internal static class MangaUpdatesFixtures
{
    public const string YoujoSenkiSearch = "mangaupdates-search-youjo-senki.json";
    public const string YoujoSenkiSeries = "mangaupdates-series-24348008396-youjo-senki.json";

    public const long YoujoSenkiSeriesId = 24348008396;

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
