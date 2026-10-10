namespace LnShelf.Client.Features.Library.Components;

public partial class ShelfFilterControls
{
    [Parameter]
    public required LibraryFilter Status { get; set; }

    [Parameter]
    public EventCallback<LibraryFilter> StatusChanged { get; set; }

    /// <summary>Null is "All".</summary>
    [Parameter]
    public Format? Format { get; set; }

    [Parameter]
    public EventCallback<Format?> FormatChanged { get; set; }

    [Parameter]
    public LibrarySort Sort { get; set; }

    [Parameter]
    public EventCallback<LibrarySort> SortChanged { get; set; }

    [Parameter]
    public LibraryView View { get; set; }

    [Parameter]
    public EventCallback<LibraryView> ViewChanged { get; set; }

    [Parameter]
    public GridDensity Density { get; set; }

    [Parameter]
    public EventCallback<GridDensity> DensityChanged { get; set; }

    /// <summary>Density is a grid setting, and the rows view has no grid.</summary>
    private bool IsDensityDisabled => View == LibraryView.Rows;

    private static readonly IReadOnlyList<SegmentedOption<LibraryView>> ViewOptions =
    [
        new(LibraryView.Rows, "Rows", "Rows — one line per series"),
        new(LibraryView.Covers, "Covers", "Covers — the cover grid"),
    ];

    private static readonly IReadOnlyList<SegmentedOption<GridDensity>> DensityOptions =
    [
        new(GridDensity.Comfortable, null, "Comfortable", IconName.SquaresFour),
        new(GridDensity.Compact, null, "Compact", IconName.GridNine),
    ];

    /// <summary>Labels say what the order is, not what the enum is called.</summary>
    private static readonly IReadOnlyList<SegmentedOption<LibrarySort>> SortOptions =
    [
        new(LibrarySort.MostBehind, "Most behind", "Furthest behind first"),
        new(LibrarySort.Alphabetical, "A–Z", "By title, A to Z"),
        new(LibrarySort.RecentlyUpdated, "Recently synced", "Most recently synced first"),
        new(LibrarySort.LastRead, "Last read", "What you read last first"),
    ];
}
