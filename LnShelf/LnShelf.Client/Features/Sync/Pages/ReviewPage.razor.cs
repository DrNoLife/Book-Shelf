namespace LnShelf.Client.Features.Sync.Pages;

public partial class ReviewPage : IDisposable
{
    /// <summary>Kept fresh by the shell (Sidebar); read again here so a deep link starts current.</summary>
    [Inject]
    public required SyncState SyncState { get; set; }

    protected override async Task OnInitializedAsync()
    {
        SyncState.Changed += OnStateChanged;
        await SyncState.LoadAsync();
    }

    public void Dispose() => SyncState.Changed -= OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
