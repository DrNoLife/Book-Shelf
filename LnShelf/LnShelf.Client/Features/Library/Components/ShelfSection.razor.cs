namespace LnShelf.Client.Features.Library.Components;

public partial class ShelfSection : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

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
        !LibraryFilterSet.IsEverything(LibraryState.SelectedFilters) || LibraryState.SelectedFormat is not null;

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

    protected override void OnInitialized()
    {
        LibraryState.Changed += OnStateChanged;
        Preferences.Changed += OnStateChanged;
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private Task OnFormatSelectedAsync(Format? format) => LibraryState.SelectFormatAsync(format);

    private Task OnFiltersSelectedAsync(IReadOnlyList<LibraryFilter> filters) => LibraryState.SelectFiltersAsync(filters);

    private Task OnFilterRemovedAsync(LibraryFilter filter) => LibraryState.RemoveFilterAsync(filter);

    private Task OnSaveDefaultAsync() => Preferences.SetDefaultLibraryFiltersAsync(LibraryState.SelectedFilters);

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
