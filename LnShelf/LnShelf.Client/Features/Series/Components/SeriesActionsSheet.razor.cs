namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesActionsSheet
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    /// <summary>"Remove from shelf…": the page owns the confirmation and the navigation away.</summary>
    [Parameter]
    public EventCallback DeleteRequested { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required VolumeSelectionState Selection { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    /// <summary>This series' view: its own override, otherwise the global preference.</summary>
    private VolumeView View => Series.VolumeViewOverride ?? Preferences.VolumeView;

    private static readonly IReadOnlyList<SegmentedOption<VolumeView>> ViewOptions =
    [
        new(VolumeView.Tiles, "Tiles", "Tiles — for this series"),
        new(VolumeView.List, "List", "List — for this series"),
    ];

    /// <summary>The run of volumes already read, plus one — the toolbar's own rule.</summary>
    private int? ReadThrough =>
        VolumeRuns.NextRunEnd(Series.Volumes, volume => volume.ReadStatus is ReadStatus.Read);

    private int? OwnedThrough => VolumeRuns.NextRunEnd(Series.Volumes, volume => volume.Owned);

    /// <summary>
    /// Sets this series' override, as the desktop toolbar does; select mode goes when the
    /// tiles it marks do.
    /// </summary>
    private async Task OnViewChosenAsync(VolumeView view)
    {
        if (view == View)
        {
            return;
        }

        if (view == VolumeView.List && Selection.IsSelectMode)
        {
            Selection.ToggleSelectMode();
        }

        await SeriesState.SetVolumeViewOverrideAsync(view);
    }

    private Task MarkReadThroughAsync() =>
        ReadThrough is { } through
            ? SeriesState.MarkThroughAsync(through, ReadStatus.Read, owned: null)
            : Task.CompletedTask;

    private Task MarkOwnedThroughAsync() =>
        OwnedThrough is { } through
            ? SeriesState.MarkThroughAsync(through, read: null, owned: true)
            : Task.CompletedTask;
}
