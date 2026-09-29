using LnShelf.Shared.Releases;

namespace LnShelf.Client.Features.Upcoming.Pages;

public partial class UpcomingPage : IDisposable
{
    [Inject]
    public required UpcomingState State { get; set; }

    /// <summary>"3 series to buy from · 2 announced" — numbers first, in the reader's terms.</summary>
    private string? Summary => State.Response is { } upcoming && !IsEmpty(upcoming)
        ? $"{upcoming.OutNow.Sum(entry => entry.VolumeNumbers.Count)} out to buy · "
          + $"{upcoming.ThisMonth.Count + upcoming.NextThreeMonths.Count + upcoming.Later.Count} dated · "
          + $"{upcoming.Announced.Sum(entry => entry.VolumeNumbers.Count)} announced"
        : null;

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private static bool IsEmpty(UpcomingResponse upcoming) =>
        upcoming.OutNow.Count == 0
        && upcoming.ThisMonth.Count == 0
        && upcoming.NextThreeMonths.Count == 0
        && upcoming.Later.Count == 0
        && upcoming.Announced.Count == 0;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
