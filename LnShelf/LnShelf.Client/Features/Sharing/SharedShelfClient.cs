using System.Net;
using LnShelf.Shared.Sharing;

namespace LnShelf.Client.Features.Sharing;

/// <summary>
/// The shared-shelf page's one call, anonymous (feature-spec §4.8).
/// </summary>
// A 404 is an answer, not a failure: the link is unknown, revoked or mistyped, and the page
// says so in place. Anything else is thrown with the sentence to print, like
// SeriesDetailClient does.
public sealed class SharedShelfClient(HttpClient httpClient)
{
    /// <returns>The shelf, or null when the link opens nothing.</returns>
    public async Task<SharedShelfResponse?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/shared/{Uri.EscapeDataString(token)}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Could not load this shelf: {(int)response.StatusCode} {response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<SharedShelfResponse>(cancellationToken);
    }
}
