using LnShelf.Shared.Stats;

namespace LnShelf.Client.Features.Stats;

/// <summary>
/// The Stats page's one fetch. Page-local, like <c>ChangelogState</c>.
/// </summary>
public sealed class StatsState(HttpClient httpClient)
{
    /// <summary>Null while loading, and while a failed load has nothing to show.</summary>
    public StatsResponse? Response { get; private set; }

    public bool LoadFailed { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            Response = await httpClient.GetFromJsonAsync<StatsResponse>("api/stats", cancellationToken);
            LoadFailed = Response is null;
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
