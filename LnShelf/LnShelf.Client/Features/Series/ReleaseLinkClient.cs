namespace LnShelf.Client.Features.Series;

/// <summary>
/// The two calls behind linking a series to its release source by hand.
/// </summary>
// Apart from SeriesDetailClient, whose every write answers with the bare series: a link answers
// with whether the refresh ran too, and the search is a read the series page only makes when
// asked. A refusal throws an HttpRequestException carrying the API's own sentence ("That
// MangaUpdates record is already linked to …"), the same convention as SeriesDetailClient.
public sealed class ReleaseLinkClient(HttpClient httpClient)
{
    /// <param name="query">Blank lets the server search the series' own title.</param>
    public async Task<ReleaseLinkCandidatesResponse> GetCandidatesAsync(
        Guid seriesId,
        string? query,
        CancellationToken cancellationToken = default)
    {
        var route = string.IsNullOrWhiteSpace(query)
            ? $"api/series/{seriesId}/release-link/candidates"
            : $"api/series/{seriesId}/release-link/candidates?q={Uri.EscapeDataString(query.Trim())}";

        using var response = await httpClient.GetAsync(route, cancellationToken);

        return await ReadAsync<ReleaseLinkCandidatesResponse>(response, "Could not search MangaUpdates", cancellationToken);
    }

    public async Task<ReleaseLinkResponse> LinkAsync(
        Guid seriesId,
        long mangaUpdatesId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/series/{seriesId}/release-link",
            new LinkReleaseSourceRequest { MangaUpdatesId = mangaUpdatesId },
            cancellationToken);

        return await ReadAsync<ReleaseLinkResponse>(response, "Could not link that", cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string refusal,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            // 429 is the per-user allowance for external calls, which a person searching
            // repeatedly can reach; it deserves words rather than a status code.
            var reason = response.StatusCode is System.Net.HttpStatusCode.TooManyRequests
                ? "Too many lookups in a minute. Wait a moment and try again."
                : await response.ReadProblemAsync(cancellationToken)
                  ?? $"{(int)response.StatusCode} {response.StatusCode}.";

            throw new HttpRequestException($"{refusal}: {reason}");
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
               ?? throw new HttpRequestException($"{refusal}: the answer was empty.");
    }
}
