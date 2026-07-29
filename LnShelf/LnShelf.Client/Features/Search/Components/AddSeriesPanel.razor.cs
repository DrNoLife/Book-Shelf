using LnShelf.Shared.Metadata;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Client.Features.Search.Components;

public partial class AddSeriesPanel : IDisposable
{
    [Inject]
    public required SeriesSearchState SearchState { get; set; }

    /// <summary>
    /// Raised after a series is added, so the page that owns the library reloads
    /// it. The panel does not touch LibraryState itself — that keeps the two
    /// feature slices from reaching into each other.
    /// </summary>
    [Parameter]
    public EventCallback OnSeriesAdded { get; set; }

    protected override void OnInitialized() => SearchState.Changed += OnSearchStateChanged;

    public void Dispose() => SearchState.Changed -= OnSearchStateChanged;

    private void OnSearchStateChanged() => InvokeAsync(StateHasChanged);

    private void OnQueryInput(ChangeEventArgs args) =>
        SearchState.Query = args.Value?.ToString() ?? string.Empty;

    private Task SearchAsync() => SearchState.SearchAsync();

    private async Task AddAsync(SeriesSearchResultResponse result)
    {
        var added = await SearchState.AddAsync(result);
        if (added is not null)
        {
            await OnSeriesAdded.InvokeAsync();
        }
    }
}
