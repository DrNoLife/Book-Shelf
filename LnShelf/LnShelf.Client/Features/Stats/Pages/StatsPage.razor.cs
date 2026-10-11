using LnShelf.Client.Features.Stats.Components;
using LnShelf.Shared.Stats;

namespace LnShelf.Client.Features.Stats.Pages;

public partial class StatsPage : IDisposable
{
    [Inject]
    public required StatsState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private static int SeriesCount(StatsResponse stats) => stats.Formats.Sum(format => format.SeriesCount);

    private static int Read(StatsResponse stats) => stats.Formats.Sum(format => format.Read);

    private static int Owned(StatsResponse stats) => stats.Formats.Sum(format => format.Owned);

    /// <summary>Owned and unread: the Backlog, in the words of the card that counts it.</summary>
    private static int Waiting(StatsResponse stats) => stats.Formats.Sum(format => format.Backlog);

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    /// <summary>"24 dated in the last 12 months · Oct so far".</summary>
    private static string ChartNote(StatsResponse stats)
    {
        var dated = stats.ReadPerMonth.Sum(month => month.Total);
        var current = stats.ReadPerMonth[^1].Month.ToString("MMM", CultureInfo.InvariantCulture);

        return $"{dated} dated in the last 12 months · {current} so far";
    }

    // The last month is the current one (StatsResponse.ReadPerMonth): still filling.
    private static List<MonthBar> ReadBars(StatsResponse stats) =>
    [
        .. stats.ReadPerMonth.Select((month, index) => new MonthBar(
            month.Month,
            month.Total,
            Count(month.Total),
            month.Total == 1 ? "1 volume" : $"{month.Total} volumes",
            IsPartial: index == stats.ReadPerMonth.Count - 1)),
    ];

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
