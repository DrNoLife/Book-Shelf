namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeGridToolbar
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required VolumeSelectionState Selection { get; set; }

    [Inject]
    public required PreferencesState Preferences { get; set; }

    /// <summary>
    /// This series' view: its own override when it has one, otherwise the global
    /// preference. The whole resolution rule, in one place.
    /// </summary>
    private VolumeView View => Series.VolumeViewOverride ?? Preferences.VolumeView;

    private bool HasSelection => Selection.SelectedVolumeIds.Count > 0;

    /// <summary>
    /// The two segments, with a title each saying whether choosing one sets an override
    /// or only follows the default.
    /// </summary>
    private IReadOnlyList<SegmentedOption<VolumeView>> ViewOptions =>
    [
        new(VolumeView.Tiles, "Tiles", ViewTitle(VolumeView.Tiles)),
        new(VolumeView.List, "List", ViewTitle(VolumeView.List)),
    ];

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
        : Selection.IsSelectMode ? "Leave select mode" : "Select volumes in bulk";

    private string MarkSelectedReadLabel => $"Mark {Selection.SelectedVolumeIds.Count} read";

    private string MarkSelectedOwnedLabel => $"Mark {Selection.SelectedVolumeIds.Count} owned";

    /// <summary>
    /// The next sensible "mark 1–N read": the run of volumes already read, plus one.
    /// Null when everything out has been read.
    /// </summary>
    private int? ReadThrough =>
        VolumeRuns.NextRunEnd(Series.Volumes, volume => volume.ReadStatus is ReadStatus.Read);

    private int? OwnedThrough => VolumeRuns.NextRunEnd(Series.Volumes, volume => volume.Owned);

    /// <summary>
    /// Choosing a view on a series sets that series' override, which is the point: the
    /// choice is about this series, not about the shelf. The global default is changed
    /// in preferences, not by looking at one series.
    /// </summary>
    private async Task OnViewChosenAsync(VolumeView view)
    {
        // Select mode belongs to the tiles; leaving it on while its chips vanish would
        // leave a selection nothing can see.
        if (view == VolumeView.List && Selection.IsSelectMode)
        {
            Selection.ToggleSelectMode();
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
            .Where(volume => Selection.SelectedVolumeIds.Contains(volume.VolumeId))
            .Select(volume => volume.Number)
            .DefaultIfEmpty(0)
            .Max();

        return highest == 0 ? Task.CompletedTask : SeriesState.MarkThroughAsync(highest, read, owned);
    }
}
