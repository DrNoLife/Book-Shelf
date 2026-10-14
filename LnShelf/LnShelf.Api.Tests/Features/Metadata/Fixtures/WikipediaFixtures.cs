using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using LnShelf.Api.Features.Metadata.Models.Wikipedia;
using Microsoft.AspNetCore.WebUtilities;

namespace LnShelf.Api.Tests.Features.Metadata.Fixtures;

/// <summary>
/// Captured real Wikipedia and Wikidata responses (testing.md #4).
/// </summary>
// Captured 2026-10-14 with the requests WikipediaClient sends (format=json, formatversion=2):
//
//   wikipedia-parse-<series>.json
//       action=parse&page=<title>&prop=wikitext&redirects=1&maxlag=5
//       Trimmed of every {{Graphic novel list}}'s Summary parameter (plot prose, most of a
//       page's bytes) and nothing else: references, headers, ISBNs and every chapter-list
//       format are verbatim. Chainsaw Man and Frieren number their lists ({{Numbered list}});
//       Spy × Family writes "*Mission: 1–5" with extras after it; Berserk leads each line with
//       its number ("* 048.") and lists its four prologue volumes without numbers; Slime's
//       page holds nine tables, the light novels first.
//   wikipedia-search-<series>.json
//       action=query&list=search&srsearch=hastemplate:"Graphic novel list" "<title>"&srlimit=5
//   wikidata-search-<series>.json
//       action=query&list=search&srsearch=haswbstatement:P8731=<AniList id>
//   wikidata-claims-<series>.json
//       action=wbgetclaims&entity=<item>&property=P2354 — only Frieren's names a list.
//   wikidata-sitelinks-frieren-list.json
//       action=wbgetentities&ids=Q123631191&props=sitelinks&sitefilter=enwiki
internal static class WikipediaFixtures
{
    public const string ChainsawManPage = "wikipedia-parse-chainsaw-man.json";
    public const string ChainsawManPageTitle = "List of Chainsaw Man chapters";
    public const string ChainsawManPageSearch = "wikipedia-search-chainsaw-man.json";
    public const string ChainsawManItemSearch = "wikidata-search-chainsaw-man.json";
    public const string ChainsawManClaims = "wikidata-claims-chainsaw-man.json";

    /// <summary>Viz's Chainsaw Man vol 1, as the page and the shelf both hold it.</summary>
    public const string ChainsawManVolumeOneIsbn = "9781974709939";

    public const string FrierenPage = "wikipedia-parse-frieren.json";
    public const string FrierenPageTitle = "List of Frieren chapters";
    public const string FrierenItemSearch = "wikidata-search-frieren.json";
    public const string FrierenClaims = "wikidata-claims-frieren.json";
    public const string FrierenListSitelinks = "wikidata-sitelinks-frieren-list.json";
    public const int FrierenAniListId = 118586;
    public const string FrierenVolumeOneIsbn = "9781974725762";

    public const string BerserkPage = "wikipedia-parse-berserk.json";
    public const string BerserkPageTitle = "List of Berserk chapters";
    public const string BerserkPageSearch = "wikipedia-search-berserk.json";
    public const string BerserkItemSearch = "wikidata-search-berserk.json";
    public const string BerserkClaims = "wikidata-claims-berserk.json";
    public const int BerserkAniListId = 30002;

    /// <summary>Dark Horse's Berserk vol 1.</summary>
    public const string BerserkVolumeOneIsbn = "9781593070205";

    public const string SpyFamilyPage = "wikipedia-parse-spy-x-family.json";
    public const string SlimePage = "wikipedia-parse-slime.json";

    /// <summary>Kodansha's Slime manga vol 1 — the second table on the page, after the light novels.</summary>
    public const string SlimeMangaVolumeOneIsbn = "9781632365064";

    /// <summary>Yen On's Slime light novel vol 1 — the first table.</summary>
    public const string SlimeLightNovelVolumeOneIsbn = "9780316414203";

    public static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fileName));

    /// <summary>A captured page's wikitext, read the way WikipediaClient reads it.</summary>
    public static string Wikitext(string fileName) =>
        JsonSerializer.Deserialize<WikipediaParseResponse>(Read(fileName), WikipediaClient.JsonOptions)?.Parse?.Wikitext
        ?? throw new InvalidOperationException($"Fixture '{fileName}' holds no wikitext.");

    /// <summary>A captured page's tables, as the parser reads them.</summary>
    public static IReadOnlyList<GraphicNovelListTable> Tables(string fileName) =>
        GraphicNovelListParser.Parse(Wikitext(fileName));
}

/// <summary>
/// Captured answers for one series across both wikis. Anything not given is answered empty —
/// no search hit, no claim, no page — the way the wikis answer a series they do not know.
/// </summary>
/// <param name="Pages">Captured pages by the title they are asked for.</param>
internal sealed record WikimediaCaptures(
    string? ItemSearch = null,
    string? Claims = null,
    string? ListSitelinks = null,
    string? PageSearch = null,
    IReadOnlyDictionary<string, string>? Pages = null)
{
    private const string EmptySearch = """{"batchcomplete":true,"query":{"search":[]}}""";
    private const string EmptyClaims = """{"claims":{}}""";
    private const string EmptyEntities = """{"entities":{},"success":1}""";
    private const string MissingPage = """{"error":{"code":"missingtitle","info":"The page you specified doesn't exist."}}""";

    /// <summary>Whether this request is one of Wikimedia's, for a handler that answers several sources.</summary>
    public static bool Answers(Uri uri) => uri.Host is "en.wikipedia.org" or "www.wikidata.org";

    public string For(Uri uri)
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        var action = query["action"].ToString();

        if (uri.Host == "www.wikidata.org")
        {
            return action switch
            {
                "wbgetclaims" => Fixture(Claims) ?? EmptyClaims,
                "wbgetentities" => Fixture(ListSitelinks) ?? EmptyEntities,
                _ => Fixture(ItemSearch) ?? EmptySearch,
            };
        }

        if (action == "parse")
        {
            return Pages?.GetValueOrDefault(query["page"].ToString()) is { } page ? WikipediaFixtures.Read(page) : MissingPage;
        }

        return Fixture(PageSearch) ?? EmptySearch;
    }

    private static string? Fixture(string? fileName) => fileName is null ? null : WikipediaFixtures.Read(fileName);
}

/// <summary>Answers Wikimedia requests from captures, recording each one.</summary>
internal sealed class CapturedWikimedia(WikimediaCaptures captures) : HttpMessageHandler
{
    public ConcurrentQueue<Uri> Requests { get; } = new();

    public WikipediaClient Client() => new(
        new HttpClient(this),
        Options.Create(new WikipediaOptions()),
        NullLogger<WikipediaClient>.Instance);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Enqueue(uri);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(captures.For(uri), Encoding.UTF8, "application/json"),
        });
    }
}
