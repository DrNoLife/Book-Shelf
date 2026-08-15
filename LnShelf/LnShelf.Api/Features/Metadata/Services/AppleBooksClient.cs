using LnShelf.Api.Features.Metadata.Models.AppleBooks;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The Apple Books transport: a typed client over the one route that answers, the keyless
/// iTunes Search endpoint.
/// </summary>
public sealed class AppleBooksClient(
    HttpClient httpClient,
    IOptions<AppleBooksOptions> options,
    ILogger<AppleBooksClient> logger)
{
    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way
    /// production does.
    /// </summary>
    // Plain web defaults, unlike Open Library's: iTunes speaks camelCase already
    // (`trackName`, `artworkUrl100`, `releaseDate`).
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// One store search for a whole series' volumes.
    /// </summary>
    /// <returns>Null when the endpoint answered with nothing parseable.</returns>
    public async Task<AppleBooksSearchResponse?> SearchAsync(
        string seriesTitle,
        Format format,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesTitle);

        var term = $"{seriesTitle} {MediumKeyword(format)}";

        var url = $"search?term={Uri.EscapeDataString(term)}"
                  + $"&media=ebook&limit={options.Value.SearchLimit}&country={StorefrontCountry}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Read and deserialize by hand rather than through ReadFromJsonAsync, which
        // validates the media type and refuses this one: the endpoint answers with
        // `Content-Type: text/javascript` — a leftover from its JSONP days — even though
        // the body is ordinary JSON.
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<AppleBooksSearchResponse>(json, JsonOptions);

        logger.LogInformation(
            "Apple Books answered {ResultCount} results for \"{SearchTerm}\"",
            parsed?.ResultCount ?? 0,
            term);

        return parsed;
    }

    /// <summary>
    /// The US storefront, because that is the market this shelf tracks.
    /// </summary>
    // Not a preference: release dates are storefront-local, so asking a different
    // storefront would answer with a different market's dates for the English editions —
    // or with no English editions at all.
    private const string StorefrontCountry = "us";

    /// <summary>
    /// The medium said out loud in the search term, which measurably improves recall.
    /// </summary>
    // Apple's relevance ranking is a plain text match, so "Slime" alone buries the light
    // novels under the manga, the spin-offs and everything else with the word in it.
    // Adding the medium is not a filter — the response still carries the wrong medium's
    // rows, and VolumeEditionMatcher is what refuses those — it just moves the wanted
    // rows into the response at all.
    private static string MediumKeyword(Format format) => format switch
    {
        Format.Manga => "manga",

        // WebNovel with the light novels: a web novel that reached a storefront reached it
        // as a light novel, which is the only shape Apple has for it.
        _ => "light novel",
    };
}
