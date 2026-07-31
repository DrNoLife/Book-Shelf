using LnShelf.Client.Features.Preferences;
using LnShelf.Shared.Preferences;
using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeGrid
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

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
    private readonly Dictionary<Guid, VolumeTile> _tiles = [];

    private PopoverPlacement? _placement;

    private bool HasSelection => SeriesState.SelectedVolumeIds.Count > 0;

    private VolumeResponse? OpenVolume =>
        SeriesState.OpenMenuVolumeId is { } openId
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

    /// <summary>
    /// The next sensible "mark 1–N read": the run of volumes already read, plus one.
    /// Null when everything out has been read.
    /// </summary>
    private int? ReadThrough => NextRunEnd(volume => volume.ReadStatus is ReadStatus.Read);

    private int? OwnedThrough => NextRunEnd(volume => volume.Owned);

    /// <summary>
    /// Says whether a choice would set an override or is only following the default.
    /// </summary>
    private string ViewTitle(VolumeView view) =>
        view == Preferences.VolumeView && Series.VolumeViewOverride is null
            ? $"{view} — the default for every series"
            : $"{view} — for this series only";

    /// <summary>
    /// Whether any volume here has a title of its own, which is what decides whether the
    /// toggle is offered at all.
    /// </summary>
    // Enrichment reaches the volumes whose ISBN is known, and publishers mostly name their
    // volumes "Volume N" anyway — so a series with nothing to show gets no control rather
    // than a control that does nothing.
    private bool HasAnyTitle => Series.Volumes.Any(volume => !string.IsNullOrWhiteSpace(volume.Title));

    /// <summary>Says what the toggle would do, and that it is a shelf-wide setting.</summary>
    private string TitlesTitle => Preferences.ShowVolumeTitles
        ? "Hide volume titles — for every series"
        : "Show volume titles where a source supplied one — for every series";

    /// <summary>Select mode paints chips on tiles, and the list view has no tiles.</summary>
    private string SelectTitle => View == VolumeView.List
        ? "Select — a tiles-view control"
        : SeriesState.IsSelectMode ? "Leave select mode" : "Select volumes in bulk";

    private string MarkSelectedReadLabel => $"Mark {SeriesState.SelectedVolumeIds.Count} read";

    private string MarkSelectedOwnedLabel => $"Mark {SeriesState.SelectedVolumeIds.Count} owned";

    /// <summary>
    /// One past the end of the leading run that already satisfies the predicate,
    /// capped at what is actually out — offering "mark 1–18 owned" on a series with 16
    /// volumes in print would be offering to own something that does not exist.
    /// </summary>
    private int? NextRunEnd(Func<VolumeResponse, bool> satisfied)
    {
        var out0 = Series.Volumes.Where(volume => volume.IsOut).OrderBy(volume => volume.Number).ToList();
        if (out0.Count == 0)
        {
            return null;
        }

        var run = 0;
        foreach (var volume in out0)
        {
            if (!satisfied(volume))
            {
                break;
            }

            run++;
        }

        return run >= out0.Count ? null : out0[run].Number;
    }

    /// <summary>
    /// Measures the tile, then opens the menu. The measurement happens before the open
    /// so the menu is drawn in its final position on its first frame — a menu that
    /// jumps after appearing reads as broken.
    /// </summary>
    private async Task OnMenuRequestedAsync(VolumeResponse volume)
    {
        if (SeriesState.OpenMenuVolumeId == volume.VolumeId)
        {
            SeriesState.CloseMenu();
            return;
        }

        var rect = await JavaScript.InvokeAsync<TileRect?>(
            "lnshelf.tileRect", _gridElement, volume.Number);

        if (rect is null)
        {
            return;
        }

        _placement = PopoverPlacer.For(rect, VolumePopover.Width, VolumePopover.Height);
        SeriesState.OpenMenu(volume);
    }

    private async Task OnMenuClosedAsync()
    {
        var closing = SeriesState.OpenMenuVolumeId;
        SeriesState.CloseMenu();

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
        SeriesState.CloseMenu();

        // The stream is capped at the endpoint's own limit; a larger file is refused
        // there rather than being read into the browser's memory first.
        await using var content = selection.File.OpenReadStream(CoverUploadLimitBytes);
        await SeriesState.SetCoverAsync(selection.Volume, content, selection.File.Name);
    }

    /// <summary>Matches the API's cap (CoverStoreOptions.MaximumUploadBytes).</summary>
    private const long CoverUploadLimitBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Choosing a view on a series sets that series' override, which is the point: the
    /// choice is about this series, not about the shelf. The global default is changed
    /// in preferences, not by looking at one series.
    /// </summary>
    private async Task OnViewChosenAsync(VolumeView view)
    {
        // Select mode belongs to the tiles; leaving it on while its chips vanish would
        // leave a selection nothing can see.
        if (view == VolumeView.List && SeriesState.IsSelectMode)
        {
            SeriesState.ToggleSelectMode();
        }

        await SeriesState.SetVolumeViewOverrideAsync(view);
    }

    private Task OnFollowGlobalAsync() => SeriesState.SetVolumeViewOverrideAsync(null);

    /// <summary>
    /// A shelf-wide preference, unlike the view, which is per series.
    /// </summary>
    // Deliberately not a per-series override. "Do I want volume titles" is a question
    // about how the reader likes to read the shelf, not about one series — and a series
    // whose volumes have no titles already shows none, which is the per-series answer
    // arriving for free.
    private Task OnTitlesToggledAsync() =>
        Preferences.SetShowVolumeTitlesAsync(!Preferences.ShowVolumeTitles);

    private Task OnReadCycledAsync(VolumeResponse volume) => SeriesState.CycleReadAsync(volume);

    private Task OnOwnedToggledAsync(VolumeResponse volume) => SeriesState.ToggleOwnedAsync(volume);

    private void OnSelectionToggled(VolumeResponse volume) => SeriesState.ToggleSelection(volume);

    private Task MarkReadThroughAsync() =>
        ReadThrough is { } through
            ? SeriesState.MarkThroughAsync(through, ReadStatus.Read, owned: null)
            : Task.CompletedTask;

    private Task MarkOwnedThroughAsync() =>
        OwnedThrough is { } through
            ? SeriesState.MarkThroughAsync(through, read: null, owned: true)
            : Task.CompletedTask;

    /// <summary>
    /// Bulk over an arbitrary selection is expressed as "through the highest selected
    /// volume", which is what the endpoint takes.
    /// </summary>
    private Task MarkSelectedReadAsync() => MarkSelectionAsync(ReadStatus.Read, owned: null);

    private Task MarkSelectedOwnedAsync() => MarkSelectionAsync(read: null, owned: true);

    private Task MarkSelectionAsync(ReadStatus? read, bool? owned)
    {
        var highest = Series.Volumes
            .Where(volume => SeriesState.SelectedVolumeIds.Contains(volume.VolumeId))
            .Select(volume => volume.Number)
            .DefaultIfEmpty(0)
            .Max();

        return highest == 0 ? Task.CompletedTask : SeriesState.MarkThroughAsync(highest, read, owned);
    }

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
