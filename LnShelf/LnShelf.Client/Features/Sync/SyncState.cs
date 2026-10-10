using System.Net;
using System.Text.Json;

namespace LnShelf.Client.Features.Sync;

/// <summary>
/// The latest sync run, for the sidebar's footer.
/// </summary>
// Its own state container rather than a field on LibraryState: the footer is part
// of the shell and shows on every page, including ones that never load a library.
public sealed class SyncState(HttpClient httpClient)
{
    public SyncSummaryResponse? Latest { get; private set; }

    /// <summary>True once the answer is known, including "never synced".</summary>
    public bool HasLoaded { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/sync/latest", cancellationToken);

            // 204 is the honest answer for "nothing has ever been synced". Anything else that
            // is not a success (a 401 mid-session, a 502 from nginx) has no JSON body to read.
            Latest = response.StatusCode is HttpStatusCode.NoContent || !response.IsSuccessStatusCode
                ? null
                : await response.Content.ReadFromJsonAsync<SyncSummaryResponse>(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            // The footer says "not synced yet" either way; there is nothing else the
            // browser can do about an API that is down.
            Latest = null;
        }
        finally
        {
            HasLoaded = true;
            Changed?.Invoke();
        }
    }
}
