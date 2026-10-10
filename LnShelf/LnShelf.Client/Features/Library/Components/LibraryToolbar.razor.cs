namespace LnShelf.Client.Features.Library.Components;

public partial class LibraryToolbar
{
    /// <summary>The localStorage name the key's open state is remembered under.</summary>
    // Per browser rather than a server preference: whether the swatches need explaining is
    // a habit of the screen in front of you, and a preferences column with its migration
    // would be ceremony for one disclosure.
    private const string KeyOpenFlag = "lnshelf.shelf-key-open";

    [Parameter]
    public string TextFilter { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> TextFilterChanged { get; set; }

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
    public GridDensity Density { get; set; }

    [Parameter]
    public EventCallback<GridDensity> DensityChanged { get; set; }

    [Parameter]
    public LibraryView View { get; set; }

    [Parameter]
    public EventCallback<LibraryView> ViewChanged { get; set; }

    /// <summary>Every filter off and the title emptied, in one go.</summary>
    [Parameter]
    public EventCallback ClearAllRequested { get; set; }

    /// <summary>"9 of 24 shown"; empty while the shelf is loading.</summary>
    [Parameter]
    public string? CountText { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private bool IsKeyOpen { get; set; }

    /// <summary>The filters that hide series — the number on the Filters button.</summary>
    // Sort, view and density reorder or redraw; they hide nothing, so they do not count. The
    // title filter is not counted either: its text is in plain sight in the field.
    private int ActiveFilterCount =>
        (Status != LibraryFilter.All ? 1 : 0) + (Format is not null ? 1 : 0);

    protected override async Task OnInitializedAsync() =>
        IsKeyOpen = await JavaScript.InvokeAsync<bool>("lnshelf.readFlag", KeyOpenFlag);

    private async Task ToggleKeyAsync()
    {
        IsKeyOpen = !IsKeyOpen;
        await JavaScript.InvokeVoidAsync("lnshelf.writeFlag", KeyOpenFlag, IsKeyOpen);
    }
}
