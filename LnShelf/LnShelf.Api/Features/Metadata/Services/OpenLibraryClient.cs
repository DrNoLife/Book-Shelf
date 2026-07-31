using System.Net;
using System.Net.Http.Json;
using LnShelf.Api.Features.Metadata.Models.OpenLibrary;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The Open Library transport: a typed client over the two routes that work.
/// </summary>
public sealed class OpenLibraryClient(
    HttpClient httpClient,
    IOptions<OpenLibraryOptions> options,
    ILogger<OpenLibraryClient> logger)
{
    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way
    /// production does.
    /// </summary>
    // Snake case: Open Library speaks `full_title`, `publish_date`, `num_found`.
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// One edition, by ISBN. The only lookup that answers unambiguously.
    /// </summary>
    /// <returns>Null when Open Library has no edition with that ISBN — a normal answer.</returns>
    public async Task<OpenLibraryEdition?> GetEditionAsync(string isbn, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);

        using var response = await httpClient.GetAsync($"isbn/{isbn}.json", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            // A genuine catalogue gap, measured at 20% of this shelf's known ISBNs —
            // information, not a failure.
            logger.LogInformation("Open Library has no edition for ISBN {Isbn}", isbn);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OpenLibraryEdition>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Candidate ISBNs for a phrase. Only the ISBN lists of the hits are of any use.
    /// </summary>
    // `fields` is passed to keep the response small: an unrestricted search.json hit
    // carries dozens of fields the shelf will never read, and asking a donated
    // catalogue for all of them per volume would be rude for no gain.
    public async Task<OpenLibrarySearchResponse?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var url = $"search.json?q={Uri.EscapeDataString(query)}"
                  + $"&fields=key,isbn&limit={options.Value.SearchResultLimit}";

        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OpenLibrarySearchResponse>(
            JsonOptions, cancellationToken);
    }

    /// <summary>
    /// Where the bytes of one cover id live. Large, which is the only size the grid
    /// renders big enough to want.
    /// </summary>
    public Uri CoverUrl(int coverId) =>
        new(options.Value.CoverEndpoint, $"b/id/{coverId}-L.jpg");
}
