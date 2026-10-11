namespace LnShelf.Client.Features.Calendar.Components;

public partial class CalendarFeedCard : IDisposable
{
    private static readonly IReadOnlyList<SegmentedOption<bool>> OnOffOptions =
    [
        new(false, "Off", "No calendar link"),
        new(true, "On", "A private link"),
    ];

    [Inject]
    public required CalendarFeedState State { get; set; }

    private bool IsConfirmingNewLink { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private static string HelpText(bool isOn) => isOn
        ? "Your release dates, in any calendar app. It updates itself after each nightly sync. Anyone with the link can see the dates."
        : "Off. Turn it on to see release dates in Google Calendar, Apple Calendar or Outlook.";

    private async Task OnIsOnChangedAsync(bool isOn)
    {
        IsConfirmingNewLink = false;
        await State.SetOnAsync(isOn);
    }

    private void OnNewLinkRequested() => IsConfirmingNewLink = true;

    private void OnNewLinkCancelled() => IsConfirmingNewLink = false;

    private async Task OnNewLinkConfirmedAsync()
    {
        IsConfirmingNewLink = false;
        await State.RegenerateAsync();
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
