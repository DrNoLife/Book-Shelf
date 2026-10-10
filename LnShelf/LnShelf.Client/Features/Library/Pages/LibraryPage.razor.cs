using LnShelf.Client.Features.Profile;

namespace LnShelf.Client.Features.Library.Pages;

public partial class LibraryPage : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    [Inject]
    public required FinishVolumeState Finishing { get; set; }

    /// <summary>Only for the phone header's avatar letter.</summary>
    [Inject]
    public required ProfileState Profile { get; set; }

    private string? AvatarInitial => Profile.User is { } user ? ProfileInitial.For(user.DisplayName) : null;

    private LibraryResponse? Library => LibraryState.Library;

    private IReadOnlyList<LibraryCardResponse> Cards => LibraryState.VisibleCards;

    /// <summary>
    /// "All 24" when nothing is hidden, "9 of 24" when a filter is narrowing.
    /// </summary>
    private string FooterCount
    {
        get
        {
            if (Library is null)
            {
                return string.Empty;
            }

            var shown = Cards.Count;
            var total = Library.Totals.SeriesCount;
            return shown == total ? $"All {total}" : $"{shown} of {total}";
        }
    }

    /// <summary>
    /// The toolbar's short count: "9 of 24 shown" while something is hidden, "24 series" when
    /// nothing is; nothing until the shelf has loaded.
    /// </summary>
    private string? ToolbarCount
    {
        get
        {
            if (Library is null)
            {
                return null;
            }

            var shown = Cards.Count;
            var total = Library.Totals.SeriesCount;
            return shown == total ? $"{total} series" : $"{shown} of {total} shown";
        }
    }

    /// <summary>A status or format filter is hiding series.</summary>
    private bool IsFiltered =>
        LibraryState.SelectedFilter != LibraryFilter.All || LibraryState.SelectedFormat is not null;

    /// <summary>The footer names the order it is actually in, not the default one.</summary>
    private string SortLabel => LibraryState.Sort switch
    {
        LibrarySort.Alphabetical => "sorted A–Z",
        LibrarySort.RecentlyUpdated => "sorted by most recently synced",
        LibrarySort.LastRead => "sorted by what you read last",
        _ => "sorted by how far behind you are",
    };

    private string GridDensityClass =>
        Preferences.GridDensity == GridDensity.Compact ? "grid grid--compact" : "grid";

    // Both lists follow the format tab but not the status filter or the text box: they
    // are about the whole shelf, not about whatever the grid below is narrowed to.
    private IReadOnlyList<ContinueReadingEntry> ContinueEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.ContinueReading(overview.Cards, take: 3) : [];

    private IReadOnlyList<OutNowEntry> OutNowEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.OutNow(overview.Cards, take: 8) : [];

    /// <summary>
    /// The phone's carousel: more than the desktop's three, because a swipe costs nothing and
    /// a row of three cards on a phone is one and a half screens.
    /// </summary>
    private IReadOnlyList<ContinueReadingEntry> UpNextEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.ContinueReading(overview.Cards, take: 12) : [];

    /// <summary>Every series behind on buying: the phone list prints three and counts the rest.</summary>
    private IReadOnlyList<OutNowEntry> AllOutNowEntries =>
        LibraryState.Overview is { } overview ? LibraryHighlights.OutNow(overview.Cards, take: overview.Cards.Count) : [];

    protected override async Task OnInitializedAsync()
    {
        LibraryState.Changed += OnStateChanged;
        Preferences.Changed += OnStateChanged;
        Finishing.Changed += OnStateChanged;
        Profile.Changed += OnStateChanged;

        // The density and sort come from the server row, so they are read before the
        // grid is drawn — a grid that re-flows a moment after landing reads as a bug.
        // Guarded, as the series page's is: the container is scoped, so a second visit
        // to the library already has them.
        if (!Preferences.HasLoaded)
        {
            await Preferences.LoadAsync();
        }
        LibraryState.SetSort(Preferences.LibrarySort);

        await LibraryState.LoadAsync();

        // After the shelf, never before it: the avatar letter is decoration, the shelf is not.
        await Profile.LoadUserAsync();
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
        Finishing.Changed -= OnStateChanged;
        Profile.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private Task OnFinishedAsync(ContinueReadingEntry entry) => Finishing.FinishAsync(entry);

    private Task OnFormatSelectedAsync(Format? format) => LibraryState.SelectFormatAsync(format);

    private Task OnFilterSelectedAsync(LibraryFilter filter) => LibraryState.SelectFilterAsync(filter);

    private void OnTextFilterChanged(string text) => LibraryState.SetTextFilter(text);

    private Task OnClearFiltersAsync() => LibraryState.ClearFiltersAsync();

    private async Task OnSortChangedAsync(LibrarySort sort)
    {
        LibraryState.SetSort(sort);
        await Preferences.SetSortAsync(sort);
    }

    private Task OnDensityChangedAsync(GridDensity density) => Preferences.SetDensityAsync(density);

    private Task OnViewChangedAsync(LibraryView view) => Preferences.SetLibraryViewAsync(view);
}
