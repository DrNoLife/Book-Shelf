using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Profile;
using LnShelf.Client.Features.Sync;

namespace LnShelf.Client.Layout;

public partial class Sidebar : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required SyncState SyncState { get; set; }

    [Inject]
    public required ImportState ImportState { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required ProfileState ProfileState { get; set; }

    [Inject]
    public required ILogger<Sidebar> Logger { get; set; }

    /// <summary>
    /// Ends the session, then leaves the app for a static page. Landing back in the app would
    /// ask the API for the library, get a 401 and sign straight back in through the provider.
    /// </summary>
    private async Task OnSignOutAsync()
    {
        try
        {
            await ProfileState.SignOutAsync(_refreshCancellation.Token);
            Navigation.NavigateTo("signed-out.html", forceLoad: true);
        }
        catch (HttpRequestException exception)
        {
            // Stay put: the session is still alive, and leaving would only look like a sign-out.
            Logger.LogWarning(exception, "Could not sign out; the session is still active");
        }
    }

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly CancellationTokenSource _refreshCancellation = new();

    private SyncSummaryResponse? LatestSync => SyncState.Latest;

    private int ReviewCount => ImportState.PendingReviewCount;

    private static DateTimeOffset Now => TimeProvider.System.GetUtcNow();

    /// <summary>
    /// One row per shelf, in the order the design lists them. "Behind on buying"
    /// is the attention shelf — the only tinted one.
    /// </summary>
    private IReadOnlyList<ShelfRow>? Shelves
    {
        get
        {
            var counts = LibraryState.Library?.Shelves;
            if (counts is null)
            {
                return null;
            }

            return
            [
                new ShelfRow("Reading", LibraryFilter.Reading, counts.Reading),
                new ShelfRow("Behind on buying", LibraryFilter.Behind, counts.BehindOnBuying, IsAttention: true),
                new ShelfRow("Backlog", LibraryFilter.Backlog, counts.BacklogVolumes, CountSuffix: " vols"),
                new ShelfRow("Up to date", LibraryFilter.UpToDate, counts.UpToDate),
                new ShelfRow("Completed", LibraryFilter.Completed, counts.Completed),
            ];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        LibraryState.Changed += OnStateChanged;
        SyncState.Changed += OnStateChanged;
        SyncState.RunFinished += OnSyncFinished;
        ImportState.Changed += OnStateChanged;

        // The shell owns these fetches: the footer and the review count show on every page,
        // including ones that never load a library or an import.
        if (!SyncState.HasLoaded)
        {
            await SyncState.LoadAsync();
        }

        if (!ImportState.HasLoadedBatches)
        {
            await ImportState.RefreshReviewCountAsync();
        }

        _ = RefreshPeriodicallyAsync(_refreshCancellation.Token);
    }

    private async Task OnSyncNowAsync()
    {
        try
        {
            await SyncState.StartAsync(_refreshCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Deliberately empty: the sidebar is being disposed.
        }
    }

    // A run can add volumes, dates and covers, and the shelf on screen was loaded before it.
    private void OnSyncFinished() => _ = InvokeAsync(async () =>
    {
        try
        {
            await LibraryState.LoadAsync(_refreshCancellation.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(exception, "Could not reload the shelf after a sync");
        }
    });

    // The shell lives for the whole session, so a run that was "in progress" at load would
    // otherwise be shown as stale — and eventually "did not finish" — long after it ended.
    // Reloading on a timer re-reads the run and re-renders the relative age too.
    private async Task RefreshPeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                // One bad tick must not end the loop: nothing awaits this task, so an
                // escaped exception would stop the refresh silently for the rest of the session.
                try
                {
                    await SyncState.LoadAsync(cancellationToken);
                    await ImportState.RefreshReviewCountAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    Logger.LogWarning(exception, "Sidebar refresh failed; trying again next tick");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Deliberately empty: the sidebar is being disposed.
        }
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        SyncState.Changed -= OnStateChanged;
        SyncState.RunFinished -= OnSyncFinished;
        ImportState.Changed -= OnStateChanged;
        _refreshCancellation.Cancel();
        _refreshCancellation.Dispose();
    }

    /// <summary>
    /// A shelf row is a library filter, so choosing one from any other page goes to the
    /// library — otherwise the click changes a filter nobody is looking at.
    /// </summary>
    private async Task SelectShelfAsync(LibraryFilter filter)
    {
        await LibraryState.SelectFilterAsync(filter);

        if (Navigation.ToBaseRelativePath(Navigation.Uri).Length > 0)
        {
            Navigation.NavigateTo("");
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private sealed record ShelfRow(
        string Label,
        LibraryFilter Filter,
        int Count,
        bool IsAttention = false,
        string CountSuffix = "");
}
