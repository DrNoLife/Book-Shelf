using LnShelf.Client.Features.Library;

namespace LnShelf.Client.Features.Search.Pages;

public partial class SearchPage : IDisposable
{
    [Inject]
    public required SeriesSearchState SearchState { get; set; }

    [Inject]
    public required LibraryState LibraryState { get; set; }

    protected override void OnInitialized() => SearchState.Changed += OnStateChanged;

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
