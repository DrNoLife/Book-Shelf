using LnShelf.Api.Features.Metadata.Models.MangaDex;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The MangaDex transport: a typed client over the two read-only routes the chapter map needs,
/// title search and the per-volume chapter aggregate.
/// </summary>
// Terms (api.mangadex.org/docs): free for projects that credit MangaDex and carry no ads. The
// shelf only reads metadata, credits MangaDex wherever its ranges show, and identifies itself
// with LnShelfUserAgent (registered in MetadataFeatureExtensions; MangaDex refuses anonymous
// agents and any Via header, which nothing here sends).
public sealed class MangaDexClient(HttpClient httpClient, ILogger<MangaDexClient> logger)
{
    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way production does.
    /// </summary>
    // Plain web defaults: MangaDex speaks camelCase (`originalLanguage`, `isUnavailable`).
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Up to ten records whose titles match, safe to erotica — every rating a published manga
    /// can carry. Pornographic records are left out: none of them is a series on a bookshelf.
    /// </summary>
    public async Task<MangaDexMangaListResponse?> SearchAsync(string title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var url = $"manga?title={Uri.EscapeDataString(title)}&limit=10"
                  + "&contentRating[]=safe&contentRating[]=suggestive&contentRating[]=erotica";

        var parsed = await GetAsync<MangaDexMangaListResponse>(url, cancellationToken);

        logger.LogInformation(
            "MangaDex answered {ResultCount} records for \"{Title}\"", parsed?.Data?.Count ?? 0, title);

        return parsed;
    }

    /// <summary>
    /// The record's chapters grouped by volume, in English only or in every language.
    /// </summary>
    // includeUnavailable always: a chapter taken down at a publisher's request is still a
    // chapter of that volume, and without it a licensed series' aggregate is near empty.
    public Task<MangaDexAggregateResponse?> GetAggregateAsync(
        Guid mangaId,
        bool englishOnly,
        CancellationToken cancellationToken)
    {
        var language = englishOnly ? "translatedLanguage[]=en&" : string.Empty;

        return GetAsync<MangaDexAggregateResponse>(
            $"manga/{mangaId}/aggregate?{language}includeUnavailable=1", cancellationToken);
    }

    private async Task<TResponse?> GetAsync<TResponse>(string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }
}
