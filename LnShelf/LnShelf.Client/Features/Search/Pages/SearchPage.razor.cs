using LnShelf.Client.Features.Library;

namespace LnShelf.Client.Features.Search.Pages;

public partial class SearchPage : IDisposable
{
    [Inject]
    public required SeriesSearchState SearchState { get; set; }

    [Inject]
    public required LibraryState LibraryState { get; set; }

    protected override async Task OnInitializedAsync()
    {
        SearchState.Changed += OnStateChanged;

        // The state container outlives the page, so results from an earlier visit are still
        // here; their "On shelf" markers may be out of date if a series was deleted since.
        if (SearchState.Results.Count > 0)
        {
            await SearchState.RefreshTrackedIdsAsync();
        }
    }

    public void Dispose() => SearchState.Changed -= OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private void OnQueryInput(ChangeEventArgs args) =>
        SearchState.Query = args.Value?.ToString() ?? string.Empty;

    private Task OnSubmitAsync() => SearchState.SearchAsync();

    /// <summary>
    /// A newly added series has to be on the shelf by the time the user goes back to
    /// it, so the library's cached response is refreshed here rather than left stale.
    /// </summary>
    private async Task OnAddedAsync(SeriesSearchResultResponse result)
    {
        var added = await SearchState.AddAsync(result);
        if (added is not null && LibraryState.Library is not null)
        {
            await LibraryState.LoadAsync();
        }
    }
}
