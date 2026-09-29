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

    private static List<MonthBar> ReadBars(StatsResponse stats) =>
    [
        .. stats.ReadPerMonth.Select(month => new MonthBar(
            month.Month,
            [
                new BarSegment(month.LightNovel, BarSegmentKind.LightNovel, $"{month.LightNovel} light novel"),
                new BarSegment(month.Manga, BarSegmentKind.Manga, $"{month.Manga} manga"),
                new BarSegment(month.WebNovel, BarSegmentKind.WebNovel, $"{month.WebNovel} web novel"),
            ],
            month.Total.ToString(CultureInfo.InvariantCulture))),
    ];

    private static List<MonthBar> SpendBars(StatsResponse stats) =>
    [
        .. stats.SpendPerMonth.Select(month => new MonthBar(
            month.Month,
            [new BarSegment(month.Total, BarSegmentKind.Plain, $"{Money(month.Total)} over {month.VolumeCount} volumes")],
            Money(month.Total))),
    ];

    // Kroner, the currency the shelf's prices are entered in, grouped with a space the way
    // feature-spec §4.7 writes "1 428 kr" — a shelf-wide total runs to thousands where a
    // series' spend line does not. Whole kroner: øre are noise at this scale.
    private static string Money(decimal amount) =>
        amount.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ') + " kr";

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
