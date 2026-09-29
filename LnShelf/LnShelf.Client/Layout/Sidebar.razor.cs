using LnShelf.Client.Features.Library;
using LnShelf.Client.Features.Sync;

namespace LnShelf.Client.Layout;

public partial class Sidebar : IDisposable
{
    [Inject]
    public required LibraryState LibraryState { get; set; }

    [Inject]
    public required SyncState SyncState { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private SyncSummaryResponse? LatestSync => SyncState.Latest;

    /// <summary>
    /// The run's own summary, one line per clause.
    /// </summary>
    // The server writes it as a single "·"-joined sentence, which is right for a log
    // and wrong for a 224px column. Splitting it is presentation, so it happens here.
    private IReadOnlyList<string> SummaryLines =>
        LatestSync is null
            ? []
            : [.. LatestSync.Summary.Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

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

    protected override async Task OnInitializedAsync()
    {
        LibraryState.Changed += OnStateChanged;
        SyncState.Changed += OnStateChanged;

        // The shell owns this fetch: the footer shows on every page, including ones
        // that never load a library.
        if (!SyncState.HasLoaded)
        {
            await SyncState.LoadAsync();
        }
    }

    public void Dispose()
    {
        LibraryState.Changed -= OnStateChanged;
        SyncState.Changed -= OnStateChanged;
    }

    /// <summary>
    /// A shelf row is a library filter, so choosing one from any other page goes to the
    /// library — otherwise the click changes a filter nobody is looking at.
    /// </summary>
    private async Task SelectShelfAsync(LibraryFilter filter)
    {
        await LibraryState.SelectFilterAsync(filter);

        if (Navigation.ToBaseRelativePath(Navigation.Uri).Length > 0)
        {
            Navigation.NavigateTo("");
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private sealed record ShelfRow(
        string Label,
        LibraryFilter Filter,
        int Count,
        bool IsAttention = false,
        string CountSuffix = "");
}
