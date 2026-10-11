using LnShelf.Client.Features.Sync;

namespace LnShelf.Client.Features.Activity.Components;

public partial class SinceLastVisit : IDisposable
{
    [Inject]
    public required ActivityState ActivityState { get; set; }

    [Inject]
    public required SyncState SyncState { get; set; }

    [Inject]
    public required ILogger<SinceLastVisit> Logger { get; set; }

    // Loaded by the panel itself on every visit to the library, so the page only places it,
    // and a sync that ran since the last visit shows up without a reload.
    protected override async Task OnInitializedAsync()
    {
        ActivityState.Changed += OnStateChanged;
        SyncState.RunFinished += OnSyncFinished;
        await ActivityState.LoadAsync();
    }

    public void Dispose()
    {
        ActivityState.Changed -= OnStateChanged;
        SyncState.RunFinished -= OnSyncFinished;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    // "Sync now" while the library is open writes news the panel loaded before; without a
    // reload the reader would read stale news and "Got it" would pass over the new.
    private void OnSyncFinished() => _ = InvokeAsync(async () =>
    {
        try
        {
            await ActivityState.LoadAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogWarning(exception, "Could not reload the activity panel after a sync");
        }
    });

    private Task DismissAsync() => ActivityState.DismissAsync();
}
