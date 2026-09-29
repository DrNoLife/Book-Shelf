namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeGrid
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required VolumeSelectionState Selection { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    /// <summary>
    /// This series' view: its own override when it has one, otherwise the global
    /// preference. The whole resolution rule, in one place.
    /// </summary>
    private VolumeView View => Series.VolumeViewOverride ?? Preferences.VolumeView;

    private ElementReference _gridElement;

    /// <summary>
    /// One reference per tile, so the menu's "Set cover" can reach into the tile that
    /// owns the file input rather than the grid duplicating one.
    /// </summary>
    // Cleared when the series changes: the keys are the previous series' volume ids, and
    // @ref never removes an entry, so without this the dictionary grew by a whole series
    // on every navigation and held its components alive with it.
    private readonly Dictionary<Guid, VolumeTile> _tiles = [];

    private Guid _tilesSeriesId;

    private PopoverPlacement? _placement;

    private VolumeResponse? OpenVolume =>
        Selection.OpenMenuVolumeId is { } openId
            ? Series.Volumes.FirstOrDefault(volume => volume.VolumeId == openId)
            : null;

    /// <summary>
    /// "17 tracked — 14 out, 2 dated, 1 announced", in the mockup's own phrasing, with
    /// the clauses that would read as zero left out.
    /// </summary>
    private string CountLine
    {
        get
        {
            var counts = Series.Counts;
            var parts = new List<string> { $"{counts.Published} out" };

            if (counts.Dated > 0)
            {
                parts.Add($"{counts.Dated} dated");
            }

            var announced = counts.NotOut - counts.Dated;
            if (announced > 0)
            {
                parts.Add($"{announced} announced");
            }

            return $"{counts.Tracked} tracked — {string.Join(", ", parts)}";
        }
    }

    protected override void OnParametersSet()
    {
        if (_tilesSeriesId == Series.SeriesId)
        {
            return;
        }

        _tilesSeriesId = Series.SeriesId;
        _tiles.Clear();
        _placement = null;
    }

    /// <summary>
    /// Measures the tile, then opens the menu. The measurement happens before the open
    /// so the menu is drawn in its final position on its first frame — a menu that
    /// jumps after appearing reads as broken.
    /// </summary>
    private async Task OnMenuRequestedAsync(VolumeResponse volume)
    {
        if (Selection.OpenMenuVolumeId == volume.VolumeId)
        {
            Selection.CloseMenu();
            return;
        }

        var rect = await JavaScript.InvokeAsync<TileRect?>(
            "lnshelf.tileRect", _gridElement, volume.Number);

        if (rect is null)
        {
            return;
        }

        // The nominal size is asked for per volume: a menu with a blurb in it is wider and
        // taller, and placing it as if it were not would hang it off the screen edge.
        _placement = PopoverPlacer.For(
            rect, VolumePopover.WidthFor(volume), VolumePopover.HeightFor(volume));
        Selection.OpenMenu(volume);
    }

    private async Task OnMenuClosedAsync()
    {
        var closing = Selection.OpenMenuVolumeId;
        Selection.CloseMenu();

        // Focus goes back where it came from, or it would land on the document body and
        // the arrow keys would stop working.
        if (closing is not null && OpenVolumeNumber(closing.Value) is { } number)
        {
            await JavaScript.InvokeVoidAsync("lnshelf.focusVolumeTile", _gridElement, number);
        }
    }

    private int? OpenVolumeNumber(Guid volumeId) =>
        Series.Volumes.FirstOrDefault(volume => volume.VolumeId == volumeId)?.Number;

    private async Task OnCoverPickRequestedAsync(VolumeResponse volume)
    {
        if (_tiles.TryGetValue(volume.VolumeId, out var tile))
        {
            await tile.OpenCoverPickerAsync();
        }
    }

    private async Task OnCoverSelectedAsync((VolumeResponse Volume, IBrowserFile File) selection)
    {
        Selection.CloseMenu();

        // Checked before opening, the way ImportState checks its CSV: OpenReadStream
        // throws an IOException for a file over its cap, and that surfaced as the Blazor
        // error bar rather than a sentence.
        if (selection.File.Size > CoverUploadLimitBytes)
        {
            SeriesState.ReportProblem(
                $"{selection.File.Name} is {selection.File.Size / 1024} KB; "
                + $"the limit for a cover is {CoverUploadLimitBytes / 1024} KB.");
            return;
        }

        // The stream is capped at the endpoint's own limit as well; a larger file is
        // refused there rather than being read into the browser's memory first.
        await using var content = selection.File.OpenReadStream(CoverUploadLimitBytes);
        await SeriesState.SetCoverAsync(selection.Volume, content, selection.File.Name);
    }

    /// <summary>Matches the API's cap (CoverStoreOptions.MaximumUploadBytes).</summary>
    private const long CoverUploadLimitBytes = 8 * 1024 * 1024;

    private Task OnReadCycledAsync(VolumeResponse volume) => SeriesState.CycleReadAsync(volume);

    private Task OnOwnedToggledAsync(VolumeResponse volume) => SeriesState.ToggleOwnedAsync(volume);

    private void OnSelectionToggled(VolumeResponse volume) => Selection.ToggleSelection(volume);

    /// <summary>
    /// Arrow-key navigation. The grid reads its own column count from the rendered
    /// layout rather than assuming one, so it stays correct at every width.
    /// </summary>
    private async Task OnNavigatedAsync((VolumeResponse Volume, string Key) navigation)
    {
        var columns = await JavaScript.InvokeAsync<int>("lnshelf.gridColumnCount", _gridElement);
        columns = Math.Max(1, columns);

        var ordered = Series.Volumes.ToList();
        var index = ordered.FindIndex(volume => volume.VolumeId == navigation.Volume.VolumeId);
        if (index < 0)
        {
            return;
        }

        var target = navigation.Key switch
        {
            "ArrowLeft" => index - 1,
            "ArrowRight" => index + 1,
            "ArrowUp" => index - columns,
            "ArrowDown" => index + columns,
            _ => index,
        };

        if (target < 0 || target >= ordered.Count)
        {
            return;
        }

        await JavaScript.InvokeVoidAsync(
            "lnshelf.focusVolumeTile", _gridElement, ordered[target].Number);
    }
}
