using LnShelf.Api.Features.Metadata.Models.Wikipedia;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The Wikimedia transport: the five read-only questions the chapter lists need, asked of
/// English Wikipedia and Wikidata, answered as clean values.
/// </summary>
// One typed client for both wikis, so they share one permit pool: they are one operator's
// servers and one allowance (mediawiki.org, Wikimedia APIs/Rate limits). Etiquette (API:
// Etiquette) is kept by construction: requests are serial, every one carries the contactable
// LnShelfUserAgent, and the page read sends maxlag=5, the non-interactive caller's courtesy.
//
// Content is CC BY-SA: the shelf credits Wikipedia, linked to the page, wherever its ranges show
// (ChapterRangeCredit).
public sealed class WikipediaClient(
    HttpClient httpClient,
    IOptions<WikipediaOptions> options,
    ILogger<WikipediaClient> logger)
{
    /// <summary>Wikidata's property "AniList manga ID".</summary>
    private const string AniListMangaIdProperty = "P8731";

    /// <summary>Wikidata's property "has list": a work's item points at its list article's item.</summary>
    private const string HasListProperty = "P2354";

    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way production does.
    /// </summary>
    // Web defaults: MediaWiki's keys are lower case ("mainsnak", "datavalue"), and the web
    // defaults read them case-insensitively into the PascalCase members.
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    private readonly WikipediaOptions _options = options.Value;

    /// <returns>The Wikidata item ("Q65240543") that names this AniList id, or null.</returns>
    public async Task<string?> FindItemByAniListIdAsync(int aniListId, CancellationToken cancellationToken)
    {
        var query = $"action=query&list=search&srsearch=haswbstatement:{AniListMangaIdProperty}={aniListId}&srlimit=1&srprop=";
        var response = await GetAsync<MediaWikiSearchResponse>(_options.WikidataEndpoint, query, response => response.Error, cancellationToken);

        return response?.Query?.Search?.FirstOrDefault()?.Title;
    }

    /// <returns>The items the work's "has list" statements point at, in statement order.</returns>
    public async Task<IReadOnlyList<string>> GetListItemsAsync(string itemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var query = $"action=wbgetclaims&entity={Uri.EscapeDataString(itemId)}&property={HasListProperty}";
        var response = await GetAsync<WikidataClaimsResponse>(_options.WikidataEndpoint, query, response => response.Error, cancellationToken);

        return
        [
            .. (response?.Claims?.GetValueOrDefault(HasListProperty) ?? [])
                .Select(claim => claim.MainSnak?.DataValue?.Value)
                .Where(value => value is { ValueKind: JsonValueKind.Object })
                .Select(value => value!.Value.TryGetProperty("id", out var id) ? id.GetString() : null)
                .OfType<string>(),
        ];
    }

    /// <returns>The English Wikipedia article title of a Wikidata item, or null when it has none.</returns>
    public async Task<string?> GetEnglishArticleTitleAsync(string itemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var query = $"action=wbgetentities&ids={Uri.EscapeDataString(itemId)}&props=sitelinks&sitefilter=enwiki";
        var response = await GetAsync<WikidataEntitiesResponse>(_options.WikidataEndpoint, query, response => response.Error, cancellationToken);

        return response?.Entities?.GetValueOrDefault(itemId)?.Sitelinks?.GetValueOrDefault("enwiki")?.Title;
    }

    /// <returns>Titles of pages that use the Graphic novel list template and mention the title, best first.</returns>
    public async Task<IReadOnlyList<string>> SearchListPagesAsync(string title, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // The title quoted, so its words are searched as a phrase rather than one by one.
        var search = Uri.EscapeDataString($"hastemplate:\"Graphic novel list\" \"{title.Replace("\"", string.Empty, StringComparison.Ordinal)}\"");
        var query = $"action=query&list=search&srsearch={search}&srlimit={limit}&srprop=";
        var response = await GetAsync<MediaWikiSearchResponse>(_options.Endpoint, query, response => response.Error, cancellationToken);

        List<string> titles = [.. (response?.Query?.Search ?? []).Select(hit => hit.Title).OfType<string>()];

        logger.LogInformation("Wikipedia proposed {PageCount} list pages for \"{Title}\"", titles.Count, title);

        return titles;
    }

    /// <returns>The page's wikitext under its title after redirects, or null when no such page exists.</returns>
    public async Task<WikipediaPage?> GetPageAsync(string pageTitle, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageTitle);

        var query = $"action=parse&page={Uri.EscapeDataString(pageTitle)}&prop=wikitext&redirects=1&maxlag=5";
        var response = await GetAsync<WikipediaParseResponse>(
            _options.Endpoint,
            query,
            // A missing page is an answer, not a failure: the remembered title has gone.
            response => response.Error is { Code: "missingtitle" } ? null : response.Error,
            cancellationToken);

        return response?.Parse is { Title: { } title, Wikitext: { } wikitext } ? new WikipediaPage(title, wikitext) : null;
    }

    /// <summary>
    /// One GET, JSON in formatversion 2, and a MediaWiki error turned into an exception.
    /// </summary>
    // MediaWiki reports most failures inside a 200 — maxlag among them — so success is judged on
    // the body as well as the status. Thrown as HttpRequestException so the chapter-map step
    // isolates it like any other source failure.
    private async Task<TResponse?> GetAsync<TResponse>(
        Uri endpoint,
        string query,
        Func<TResponse, MediaWikiError?> errorOf,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        var url = new Uri($"{endpoint}?{query}&format=json&formatversion=2");

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var parsed = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);

        if (parsed is not null && errorOf(parsed) is { } error)
        {
            throw new HttpRequestException($"{endpoint.Host} answered \"{error.Code}\": {error.Info}");
        }

        return parsed;
    }
}
