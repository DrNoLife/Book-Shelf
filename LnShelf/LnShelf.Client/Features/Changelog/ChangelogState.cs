using System.Net.Http.Json;
using LnShelf.Shared.Changelog;

namespace LnShelf.Client.Features.Changelog;

/// <summary>
/// The changelog page's one fetch. Unlike <c>SyncState</c> this lives only on that page —
/// nothing else in the shell needs the release notes or the version they carry.
/// </summary>
public sealed class ChangelogState(HttpClient httpClient)
{
    /// <summary>Null while loading, and while a failed load has nothing to show.</summary>
    public ChangelogResponse? Response { get; private set; }

    public bool LoadFailed { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            Response = await httpClient.GetFromJsonAsync<ChangelogResponse>("api/changelog", cancellationToken);
            LoadFailed = Response is null;
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; there is nothing else the browser
            // can do about an API that is down.
            LoadFailed = true;
        }
        finally
        {
            Changed?.Invoke();
        }
    }
}
