using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The MangaUpdates transport: a typed client over its v1 REST API.
/// </summary>
public sealed class MangaUpdatesClient(HttpClient httpClient, ILogger<MangaUpdatesClient> logger)
{
    /// <summary>
    /// Public so the fixture tests deserialize captured responses exactly the way
    /// production does.
    /// </summary>
    // Snake case, unlike AniList's camel case: MangaUpdates speaks `series_id`,
    // `total_hits`, `per_page`. Left on the Web defaults, every field silently
    // deserialized to its default and the client returned a series with id 0 —
    // which is exactly what the captured-response tests exist to catch.
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<MangaUpdatesSearchResponse?> SearchAsync(
        MangaUpdatesSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            "series/search", request, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MangaUpdatesSearchResponse>(
            JsonOptions, cancellationToken);
    }

    /// <returns>Null when MangaUpdates answers 404 — a normal answer, not a failure.</returns>
    public async Task<MangaUpdatesSeriesRecord?> GetSeriesAsync(
        long seriesId,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"series/{seriesId}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            logger.LogInformation("MangaUpdates has no series with id {MangaUpdatesId}", seriesId);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MangaUpdatesSeriesRecord>(
            JsonOptions, cancellationToken);
    }
}
