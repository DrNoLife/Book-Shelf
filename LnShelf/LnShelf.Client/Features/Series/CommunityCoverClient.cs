namespace LnShelf.Client.Features.Series;

/// <summary>
/// The two calls the community-cover picker makes besides picking: what others share for a
/// volume, and hiding one image from the reader's own list.
/// </summary>
// Apart from SeriesDetailClient, whose every write answers with the bare series: neither call
// here changes the series. A refusal throws an HttpRequestException carrying a sentence to
// print, the same convention as SeriesDetailClient.
public sealed class CommunityCoverClient(HttpClient httpClient)
{
    public async Task<CommunityCoversResponse> GetCandidatesAsync(
        Guid volumeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/volumes/{volumeId}/community-covers", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw Refused("Could not load the shared covers", response);
        }

        return await response.Content.ReadFromJsonAsync<CommunityCoversResponse>(cancellationToken)
               ?? throw new HttpRequestException("Could not load the shared covers: the answer was empty.");
    }

    /// <summary>Stops offering this image to the reader. Idempotent on the server.</summary>
    public async Task HideAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsync(
            $"api/community-covers/{contentHash}/hide", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw Refused("Could not hide that cover", response);
        }
    }

    private static HttpRequestException Refused(string what, HttpResponseMessage response) =>
        new($"{what}: {(int)response.StatusCode} {response.StatusCode}.");
}
