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

    // Which of the two slow calls is running, so only its button shows the wait. The state's
    // IsBusy still disables every button on the card.
    private bool _isEnabling;
    private bool _isTesting;

    // Nothing awaited before the call (see NotificationsState.EnableAsync); setting a field is
    // synchronous, so the tap that asked still opens the permission prompt.
    private async Task OnEnableAsync()
    {
        _isEnabling = true;

        try
        {
            await State.EnableAsync();
        }
        finally
        {
            _isEnabling = false;
        }
    }

    private async Task OnSendTestAsync()
    {
        _isTesting = true;

        try
        {
            await State.SendTestAsync();
        }
        finally
        {
            _isTesting = false;
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
