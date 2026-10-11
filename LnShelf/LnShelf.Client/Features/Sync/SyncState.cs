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

    /// <summary>True from the moment a sync is asked for until the run it started has finished.</summary>
    public bool IsFollowingRun { get; private set; }

    /// <summary>
    /// One quiet line about the last "Sync now", when there is something to say: the shelf is
    /// already syncing, or it has synced as often this hour as it may. Null otherwise.
    /// </summary>
    public string? Notice { get; private set; }

    public event Action? Changed;

    /// <summary>Raised once when a run started from here has finished.</summary>
    public event Action? RunFinished;

    // Long enough not to hammer the API while a run takes its minutes, short enough that the
    // sidebar flips from "in progress" to "Synced just now" soon after it ends.
    private static readonly TimeSpan FollowInterval = TimeSpan.FromSeconds(10);

    // A run that has not finished in this long is left to the sidebar's own five-minute
    // refresh; following it for ever would leave a loop running the whole session.
    private static readonly TimeSpan FollowLimit = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Starts a sync of the reader's shelf now — the same run the nightly job makes — and
    /// follows it until it has finished.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsFollowingRun)
        {
            return;
        }

        var requestedAt = TimeProvider.System.GetUtcNow();
        var before = Latest;
        IsFollowingRun = true;
        Notice = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.PostAsync("api/sync", content: null, cancellationToken);

            Notice = response.StatusCode switch
            {
                HttpStatusCode.Accepted => null,
                HttpStatusCode.Conflict => "Already syncing",
                HttpStatusCode.TooManyRequests => "Synced enough for this hour",
                _ => "Could not start a sync",
            };

            // A 409 means a run is under way, so it is followed just the same.
            if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.Conflict)
            {
                // Not the caller's token: the phone starts a sync from the profile page and the
                // reader usually leaves it, which would end the follow and with it the shelf
                // reload. The follow is bounded by FollowLimit and outlives no session.
                await FollowRunAsync(before, requestedAt, CancellationToken.None);
            }
        }
        catch (HttpRequestException)
        {
            Notice = "Could not start a sync";
        }
        finally
        {
            IsFollowingRun = false;
            Changed?.Invoke();
        }
    }

    private async Task FollowRunAsync(
        SyncSummaryResponse? before,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var deadline = requestedAt + FollowLimit;

        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            await Task.Delay(FollowInterval, cancellationToken);
            await LoadAsync(cancellationToken);

            // Done when a run other than the finished one shown before has finished — or the one
            // shown before, if it was still going. Run ids rather than clocks, so a browser
            // whose clock disagrees with the server's cannot wait for ever or stop early.
            if (Latest is { FinishedAt: not null } run
                && (before is null || run.RunId != before.RunId || before.FinishedAt is null))
            {
                RunFinished?.Invoke();
                return;
            }
        }
    }

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
