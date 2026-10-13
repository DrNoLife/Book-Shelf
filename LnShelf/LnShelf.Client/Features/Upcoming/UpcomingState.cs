using LnShelf.Shared.Releases;

namespace LnShelf.Client.Features.Upcoming;

/// <summary>
/// The Upcoming page's one fetch, and the format it is scoped to.
/// </summary>
// Page-local, like ChangelogState: nothing else in the shell reads the upcoming groups.
// The format is kept here rather than on the page so it survives a round trip to a series
// and back — the same courtesy LibraryState extends to its tabs.
public sealed class UpcomingState(HttpClient httpClient) : IDisposable
{
    private CancellationTokenSource? _loadCancellation;

    /// <summary>Null while loading, and while a failed load has nothing to show.</summary>
    public UpcomingResponse? Response { get; private set; }

    public bool LoadFailed { get; private set; }

    /// <summary>Null is "All".</summary>
    public Format? SelectedFormat { get; private set; }

    public event Action? Changed;

    public Task SelectFormatAsync(Format? format)
    {
        SelectedFormat = format;
        return LoadAsync();
    }

    /// <summary>Message of a refused or failed "notify me when dated" write, or null.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Asks to be told, or not, when one announced volume gets a date, then re-reads the
    /// groups so the line shows what the server now holds.
    /// </summary>
    public async Task SetNotifyWhenDatedAsync(Guid volumeId, bool notifyWhenDated, CancellationToken cancellationToken = default)
    {
        Message = null;

        try
        {
            using var response = await httpClient.PutAsJsonAsync(
                $"api/volumes/{volumeId}/notify-when-dated",
                new SetVolumeNotifyWhenDatedRequest { NotifyWhenDated = notifyWhenDated },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = $"Could not save that: {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; nothing was changed.
            Message = "Could not reach the API. Nothing was changed.";
        }

        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        // A tab clicked while the previous answer is still in flight must not be overtaken
        // by it: the older response is cancelled rather than allowed to land last and paint
        // the wrong format under the active tab.
        if (_loadCancellation is not null)
        {
            await _loadCancellation.CancelAsync();
            _loadCancellation.Dispose();
        }

        var cancellation = _loadCancellation = new CancellationTokenSource();
        LoadFailed = false;
        Changed?.Invoke();

        try
        {
            var url = SelectedFormat is { } format ? $"api/releases/upcoming?format={format}" : "api/releases/upcoming";
            Response = await httpClient.GetFromJsonAsync<UpcomingResponse>(url, cancellation.Token);
            LoadFailed = Response is null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Superseded by a newer load, which owns the page now.
            return;
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; there is nothing else the browser can
            // do about an API that is down.
            LoadFailed = true;
        }

        Changed?.Invoke();
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }
}
