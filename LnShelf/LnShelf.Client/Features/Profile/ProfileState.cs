using System.Net;
using LnShelf.Shared.Auth;
using LnShelf.Shared.Stats;

namespace LnShelf.Client.Features.Profile;

/// <summary>
/// What the profile page shows: who is signed in, the stats it reuses, and the series being read.
/// </summary>
// Three fetches that do not depend on each other, started together. The stats are the Stats
// page's own endpoint, so the totals here are the totals there.
public sealed class ProfileState(HttpClient httpClient)
{
    public CurrentUserResponse? User { get; private set; }

    public StatsResponse? Stats { get; private set; }

    /// <summary>The series marked Reading, as the library's cards.</summary>
    public IReadOnlyList<LibraryCardResponse> Reading { get; private set; } = [];

    public bool LoadFailed { get; private set; }

    public event Action? Changed;

    /// <summary>Ends the session. Throws <see cref="HttpRequestException"/> when the API cannot be reached.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync("api/auth/logout", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Deletes the signed-in user's account and everything on it. Returns the status code so
    /// the card can tell "gone" (204) from a refusal, without a failed delete reading as an error.
    /// </summary>
    public async Task<HttpStatusCode> DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync("api/auth/account", cancellationToken);
        return response.StatusCode;
    }

    /// <summary>
    /// Only who is signed in, for the library's avatar on a phone. Skipped when the user is
    /// already known; a failure leaves the avatar on its fallback rather than reporting.
    /// </summary>
    public async Task LoadUserAsync(CancellationToken cancellationToken = default)
    {
        if (User is not null)
        {
            return;
        }

        try
        {
            User = await httpClient.GetFromJsonAsync<CurrentUserResponse>("api/auth/me", cancellationToken);
        }
        catch (HttpRequestException)
        {
            // The avatar is a shortcut to the profile, not information; without a name it
            // shows the person icon and still works.
            return;
        }

        Changed?.Invoke();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            var user = httpClient.GetFromJsonAsync<CurrentUserResponse>("api/auth/me", cancellationToken);
            var stats = httpClient.GetFromJsonAsync<StatsResponse>("api/stats", cancellationToken);
            var reading = httpClient.GetFromJsonAsync<LibraryResponse>(
                $"api/library?filter={LibraryFilter.Reading}", cancellationToken);

            await Task.WhenAll(user, stats, reading);

            User = await user;
            Stats = await stats;
            Reading = (await reading)?.Cards ?? [];
            LoadFailed = User is null || Stats is null;
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; there is nothing else the browser can
            // do about an API that is down.
            LoadFailed = true;
        }
        finally
        {
            Changed?.Invoke();
        }
    }
}
