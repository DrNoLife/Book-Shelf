using Microsoft.AspNetCore.Components.Routing;

namespace LnShelf.Client.Features.Library.Components;

public partial class ShelfSection : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    /// <summary>For the links into the shelf (LibraryLinks): a genre tag or an author.</summary>
    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _section;

    /// <summary>A link was just applied: bring the shelf into view once it has drawn.</summary>
    private bool _scrollToShelf;

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

    /// <summary>A status, format or genre filter is hiding series.</summary>
    private bool IsFiltered =>
        !LibraryFilterSet.IsEverything(LibraryState.SelectedFilters)
        || LibraryState.SelectedFormat is not null
        || LibraryState.SelectedGenres.Count > 0;

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
        Navigation.LocationChanged += OnLocationChanged;

        ApplyLink(Navigation.Uri);
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        Preferences.Changed -= OnStateChanged;
        Navigation.LocationChanged -= OnLocationChanged;
    }

    /// <summary>
    /// A genre tag or an author clicked while the library is already open arrives as a new
    /// query on the same page, not as a new page.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => ApplyLink(args.Location);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Waits for the shelf to load: the sections above it change height as they fill in.
        if (!_scrollToShelf || Library is null)
        {
            return;
        }

        _scrollToShelf = false;
        await JavaScript.InvokeVoidAsync("lnshelf.scrollIntoView", _section);
    }

    private void ApplyLink(string uri)
    {
        var link = LibraryLinks.Read(uri);
        if (link.IsEmpty)
        {
            return;
        }

        LibraryState.ApplyLink(link.Genre, link.Text);
        _scrollToShelf = true;

        // One-shot: the query comes off the address straight away (replacing the history
        // entry, so Back does not land on it again). Left in place, Back from a series or a
        // reload would put the filter back after the reader had cleared it.
        var withoutLink = Navigation.GetUriWithQueryParameters(uri, new Dictionary<string, object?>
        {
            [LibraryLinks.GenreParameter] = null,
            [LibraryLinks.TextParameter] = null,
        });
        Navigation.NavigateTo(withoutLink, replace: true);
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
