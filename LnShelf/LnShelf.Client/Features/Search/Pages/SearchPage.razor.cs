using LnShelf.Client.Features.Import;
using LnShelf.Client.Features.Library;

namespace LnShelf.Client.Features.Search.Pages;

public partial class SearchPage : IDisposable
{
    // The segmented control's value type must not be nullable, so "All" is the empty key.
    private const string AllKey = "";

    [Inject]
    public required SeriesSearchState SearchState { get; set; }

    [Inject]
    public required LibraryState LibraryState { get; set; }

    /// <summary>Only for the Import tab's "N to review"; the sidebar keeps it loaded.</summary>
    [Inject]
    public required ImportState ImportState { get; set; }

    [Inject]
    public required NavigationTrail NavigationTrail { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    // The result the reader picked. Null until they pick one; the preview then shows the
    // first result, so a wide screen never opens on an empty panel.
    private SeriesSearchResultResponse? _chosenResult;
    private bool _isSheetOpen;

    // The work whose add-all is running, so only its button shows the progress.
    private SearchWork? _batchWork;

    private ElementReference _previewPanel;
    private ElementReference _resultsSection;

    /// <summary>The entry the preview shows: the one picked, or else the first one listed.</summary>
    private SearchWorkEntry? SelectedEntry
    {
        get
        {
            var entries = SearchState.Works.SelectMany(work => work.Entries).ToList();

            return entries.FirstOrDefault(entry => ReferenceEquals(entry.Result, _chosenResult))
                   ?? entries.FirstOrDefault();
        }
    }

    private string FormatKey => SearchState.FormatFilter?.ToString() ?? AllKey;

    /// <summary>All · LN · Manga · WN, each with how many results it holds once there are results.</summary>
    private IReadOnlyList<SegmentedOption<string>> FormatOptions =>
    [
        Option(AllKey, "All", "All formats", format: null),
        Option(nameof(Format.LightNovel), "LN", "Light novels", Format.LightNovel),
        Option(nameof(Format.Manga), "Manga", "Manga", Format.Manga),
        Option(nameof(Format.WebNovel), "WN", "Web novels", Format.WebNovel),
    ];

    /// <summary>Names whichever catalogue actually answered: the fallback is not AniList.</summary>
    private string SourceNote => SearchState.HasSearched && SearchState.Source is not MetadataSourceName.AniList
        ? $"Searched {SearchState.Source.DisplayName}. Light novel and manga are tracked separately."
        : "Searches AniList and MangaUpdates. Light novel and manga are tracked separately.";

    private string ResultCountLine
    {
        get
        {
            var results = SearchState.VisibleResultCount == 1 ? "1 result" : $"{SearchState.VisibleResultCount} results";
            var works = SearchState.Works.Count == 1 ? "1 work" : $"{SearchState.Works.Count} works";

            return $"{results}, grouped into {works}";
        }
    }

    /// <summary>Nothing at all, or nothing in the chosen format.</summary>
    private string EmptyLine => SearchState.Results.Count > 0 && SearchState.FormatFilter is { } format
        ? $"No {FormatWords(format)} results."
        : $"{SearchState.Source.DisplayName} knows nothing by that name.";

    protected override async Task OnInitializedAsync()
    {
        SearchState.Changed += OnStateChanged;
        ImportState.Changed += OnStateChanged;

        // The state container outlives the page, so results from an earlier visit are still
        // here; their "On shelf" markers may be out of date if a series was deleted since.
        if (SearchState.Results.Count > 0)
        {
            await SearchState.RefreshTrackedIdsAsync();
        }
    }

    public void Dispose()
    {
        SearchState.Changed -= OnStateChanged;
        ImportState.Changed -= OnStateChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    /// <summary>Typing only edits the query; clearing it (× or Escape) clears the results too.</summary>
    private async Task OnQueryChangedAsync(string value)
    {
        SearchState.Query = value;

        if (value.Length == 0)
        {
            _chosenResult = null;
            await SearchState.SearchAsync();
        }
    }

    private async Task OnSubmitAsync()
    {
        _chosenResult = null;
        _isSheetOpen = false;
        await SearchState.SearchAsync();
    }

    private void OnFormatChosen(string key) =>
        SearchState.SetFormatFilter(key.Length == 0 ? null : Enum.Parse<Format>(key));

    /// <summary>
    /// A row was picked: the side panel follows it — or, where the panel has no room beside
    /// the results and the stylesheet hides it, the preview opens as a bottom sheet.
    /// </summary>
    // Asked per press rather than once: the same window crosses the line when a tablet turns
    // or a desktop window is narrowed.
    private async Task OnSelectedAsync(SearchWorkEntry entry)
    {
        _chosenResult = entry.Result;
        _isSheetOpen = !await JavaScript.InvokeAsync<bool>("lnshelf.isDisplayed", _previewPanel);
    }

    /// <summary>The sheet cannot know what opened it, so the page hands focus back to the row.</summary>
    private async Task CloseSheetAsync()
    {
        _isSheetOpen = false;
        StateHasChanged();
        await JavaScript.InvokeVoidAsync("lnshelf.focusCurrent", _resultsSection);
    }

    /// <summary>A row's own Add: on the shelf at once, nothing read or owned.</summary>
    private async Task OnQuickAddAsync(SearchWorkEntry entry)
    {
        var added = await SearchState.AddAsync(entry.Result);
        await RefreshLibraryAsync(added is not null);
    }

    private async Task OnPreviewAddAsync(StartingVolumes start)
    {
        if (SelectedEntry is not { } selected)
        {
            return;
        }

        var added = await SearchState.AddAsync(selected.Result, start);
        await RefreshLibraryAsync(added is not null);
    }

    private async Task OnAddAllAsync(SearchWork work, IReadOnlyList<SearchWorkEntry> entries)
    {
        _batchWork = work;

        try
        {
            var addedCount = await SearchState.AddAllAsync(entries);
            await RefreshLibraryAsync(addedCount > 0);
        }
        finally
        {
            _batchWork = null;
        }
    }

    /// <summary>
    /// A newly added series has to be on the shelf by the time the user goes back to it, so
    /// the library's cached response is refreshed here rather than left stale.
    /// </summary>
    private async Task RefreshLibraryAsync(bool anythingAdded)
    {
        if (anythingAdded && LibraryState.Library is not null)
        {
            await LibraryState.LoadAsync();
        }
    }

    /// <summary>Back to wherever the reader came from; the library when the app opened here.</summary>
    private void Cancel() =>
        Navigation.NavigateTo(NavigationTrail.LastLocationOutside("search") ?? string.Empty);

    private SegmentedOption<string> Option(string key, string label, string title, Format? format) =>
        new(key, label, title, Detail: SearchState.HasSearched
            ? SearchState.CountOf(format).ToString(CultureInfo.InvariantCulture)
            : null);

    private static string FormatWords(Format format) => format switch
    {
        Format.LightNovel => "light novel",
        Format.Manga => "manga",
        Format.WebNovel => "web novel",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
