using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Client.Features.Library.Pages;

public partial class LibraryPage : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    private LibraryResponse? Library => LibraryState.Library;

    /// <summary>
    /// "All 24" when nothing is hidden, "9 of 24" when the filter is narrowing.
    /// </summary>
    private string FooterCount
    {
        get
        {
            if (Library is null)
            {
                return string.Empty;
            }

            var shown = Library.Cards.Count;
            var total = Library.Totals.SeriesCount;
            return shown == total ? $"All {total}" : $"{shown} of {total}";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        LibraryState.Changed += OnLibraryStateChanged;
        await LibraryState.LoadAsync();
    }

    public void Dispose() => LibraryState.Changed -= OnLibraryStateChanged;

    private void OnLibraryStateChanged() => InvokeAsync(StateHasChanged);

    /// <summary>A series that was just added has to appear without a page reload.</summary>
    private Task OnSeriesAddedAsync() => LibraryState.LoadAsync();

    private Task OnFormatSelectedAsync(Format? format) => LibraryState.SelectFormatAsync(format);

    private Task OnFilterSelectedAsync(LibraryFilter filter) => LibraryState.SelectFilterAsync(filter);
}
