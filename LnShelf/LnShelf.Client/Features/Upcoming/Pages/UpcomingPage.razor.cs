using LnShelf.Shared.Releases;

namespace LnShelf.Client.Features.Upcoming.Pages;

public partial class UpcomingPage : IDisposable
{
    [Inject]
    public required UpcomingState State { get; set; }

    [Inject]
    public required BuyVolumeState BuyState { get; set; }

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    /// <summary>"3 out that you don't own · 2 dated · 4 announced" — numbers first, in the reader's terms.</summary>
    private string? Summary => State.Response is { } upcoming && !IsEmpty(upcoming)
        ? $"{ToBuyText(upcoming.OutNow.Sum(entry => entry.VolumeNumbers.Count))} · "
          + $"{upcoming.ThisMonth.Count + upcoming.NextThreeMonths.Count + upcoming.Later.Count} dated · "
          + $"{upcoming.Announced.Sum(entry => entry.VolumeNumbers.Count)} announced"
        : null;

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        BuyState.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        BuyState.Changed -= OnStateChanged;
        BuyState.Forget();
    }

    private static string ToBuyText(int toBuy) => toBuy == 0
        ? "You own everything that's out"
        : $"{toBuy} out that you don't own";

    // A series bought out this visit keeps the page from reading "caught up": its card still
    // carries the Undo.
    private bool IsEmpty(UpcomingResponse upcoming) =>
        BuyState.Bought.Count == 0
        && upcoming.OutNow.Count == 0
        && upcoming.ThisMonth.Count == 0
        && upcoming.NextThreeMonths.Count == 0
        && upcoming.Later.Count == 0
        && upcoming.Announced.Count == 0;

    /// <summary>
    /// The out-now lines, then the series this visit has bought out — kept as "On your shelf
    /// now" with their Undo, because the response no longer carries them.
    /// </summary>
    private List<OutNowCardModel> OutNowCards(UpcomingResponse upcoming)
    {
        var open = upcoming.OutNow.Select(entry => new OutNowCardModel(entry, IsDone: false)).ToList();
        var openSeries = upcoming.OutNow.Select(entry => entry.SeriesId).ToHashSet();

        var done = BuyState.Bought
            .Where(bought => !openSeries.Contains(bought.Entry.SeriesId))
            .Where(bought => State.SelectedFormat is null || bought.Entry.Format == State.SelectedFormat)
            .Select(bought => new OutNowCardModel(bought.Entry, IsDone: true));

        return [.. open, .. done];
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private sealed record OutNowCardModel(UpcomingEntryResponse Entry, bool IsDone);
}
