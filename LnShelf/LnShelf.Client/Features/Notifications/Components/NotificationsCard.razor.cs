namespace LnShelf.Client.Features.Notifications.Components;

public partial class NotificationsCard : IDisposable
{
    [Inject]
    public required NotificationsState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    // Straight through, with nothing awaited first: see NotificationsState.EnableAsync.
    private Task OnEnableAsync() => State.EnableAsync();

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
