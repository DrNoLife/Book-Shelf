namespace LnShelf.Client.Features.Calendar.Components;

public partial class CalendarSubscribe : IDisposable
{
    [Inject]
    public required CalendarFeedState State { get; set; }

    private bool IsOpen { get; set; }

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    public void Dispose() => State.Changed -= OnStateChanged;

    // Loaded on first open rather than with the page: most visits to Upcoming never ask.
    private async Task ToggleAsync()
    {
        IsOpen = !IsOpen;

        if (IsOpen && State.Feed is null)
        {
            await State.LoadAsync();
        }
    }

    private Task TurnOnAsync() => State.SetOnAsync(true);

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
