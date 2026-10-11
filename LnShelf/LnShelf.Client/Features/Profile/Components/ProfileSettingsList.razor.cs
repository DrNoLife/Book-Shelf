using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Sync;

namespace LnShelf.Client.Features.Profile.Components;

public partial class ProfileSettingsList : IDisposable
{
    /// <summary>The signed-in address, printed beside Sign out so it says who is leaving.</summary>
    [Parameter]
    public required string Email { get; set; }

    [Inject]
    public required ProfileState State { get; set; }

    /// <summary>Kept fresh by the shell (Sidebar), which stays mounted on a phone, only hidden.</summary>
    [Inject]
    public required SyncState SyncState { get; set; }

    [Inject]
    public required ImportState ImportState { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private readonly CancellationTokenSource _disposal = new();
    private bool _isSigningOut;
    private string? _message;

    private static DateTimeOffset Now => TimeProvider.System.GetUtcNow();

    private int ReviewCount => ImportState.PendingReviewCount;

    private string SyncHeadline => SyncState.Latest is { } latest
        ? latest.Headline(Now) + (latest.Attention is { } attention ? $" · {attention}" : null)
        : SyncStatusText.NeverSyncedHeadline;

    private string SyncDetail => SyncState.Latest?.Detail ?? SyncStatusText.NeverSyncedDetail;

    private SignalTone SyncTone => SyncState.Latest?.Tone(Now) ?? SignalTone.Quiet;

    protected override void OnInitialized()
    {
        SyncState.Changed += OnStateChanged;
        ImportState.Changed += OnStateChanged;
    }

    private async Task SyncNowAsync()
    {
        try
        {
            await SyncState.StartAsync(_disposal.Token);
        }
        catch (OperationCanceledException)
        {
            // Deliberately empty: the page is being left.
        }
    }

    /// <summary>
    /// Ends the session, then leaves for the static signed-out page — landing back in the app
    /// would ask the API for the library, get a 401 and sign straight back in.
    /// </summary>
    private async Task SignOutAsync()
    {
        _isSigningOut = true;
        _message = null;

        try
        {
            await State.SignOutAsync(_disposal.Token);
            Navigation.NavigateTo("signed-out.html", forceLoad: true);
        }
        catch (HttpRequestException)
        {
            // The session is still alive, so staying put is the honest answer.
            _message = "Could not sign out. Try again in a moment.";
            _isSigningOut = false;
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        SyncState.Changed -= OnStateChanged;
        ImportState.Changed -= OnStateChanged;
        _disposal.Cancel();
        _disposal.Dispose();
    }
}
