using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeGrid
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private ElementReference _gridElement;

    private bool HasSelection => SeriesState.SelectedVolumeIds.Count > 0;

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
