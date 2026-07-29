using LnShelf.Client.Features.Library;
using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Client.Layout;

public partial class Sidebar : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    /// <summary>
    /// One row per shelf, in the order the design lists them. "Behind on buying"
    /// is the attention shelf — the only tinted one.
    /// </summary>
    private IReadOnlyList<ShelfRow>? Shelves
    {
        get
        {
            var counts = LibraryState.Library?.Shelves;
            if (counts is null)
            {
                return null;
            }

            return
            [
                new ShelfRow("Reading", LibraryFilter.Reading, counts.Reading),
                new ShelfRow("Behind on buying", LibraryFilter.Behind, counts.BehindOnBuying, IsAttention: true),
                new ShelfRow("Backlog", LibraryFilter.Backlog, counts.BacklogVolumes, CountSuffix: " vols"),
                new ShelfRow("Up to date", LibraryFilter.UpToDate, counts.UpToDate),
                new ShelfRow("Completed", LibraryFilter.Completed, counts.Completed),
            ];
        }
    }

    protected override void OnInitialized() =>
        LibraryState.Changed += OnLibraryStateChanged;

    public void Dispose() =>
        LibraryState.Changed -= OnLibraryStateChanged;

    private void OnLibraryStateChanged() => InvokeAsync(StateHasChanged);

    private sealed record ShelfRow(
        string Label,
        LibraryFilter Filter,
        int Count,
        bool IsAttention = false,
        string CountSuffix = "");
}
