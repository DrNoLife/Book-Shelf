namespace LnShelf.Client.Features.Series.Components;

public partial class TrackingStatusControl
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    /// <summary>
    /// Injected rather than handed a callback, which is what every other control on this
    /// page does (VolumeGridToolbar): the write is about the series the page is holding,
    /// and routing it up through the hero would only move the same call.
    /// </summary>
    [Inject]
    public required SeriesState SeriesState { get; set; }

    /// <summary>
    /// The four tracking statuses in the domain's own words (CLAUDE.md — Series status).
    /// </summary>
    private static readonly IReadOnlyList<SegmentedOption<TrackingStatus>> StatusOptions =
    [
        new(TrackingStatus.Reading, "Reading", "Reading"),
        new(TrackingStatus.PlanToStart, "Plan to start", "Plan to start"),
        new(TrackingStatus.Completed, "Completed", "Completed"),
        new(TrackingStatus.Dropped, "Dropped", "Dropped"),
    ];

    /// <summary>
    /// Saves the chosen status, and nothing else — a null note leaves the note alone.
    /// </summary>
    // Choosing what is already chosen writes nothing: a round trip that cannot change
    // anything is not worth the tiles going inert for.
    private Task OnChosenAsync(TrackingStatus status) =>
        status != Series.TrackingStatus
            ? SeriesState.SetTrackingStatusAsync(status)
            : Task.CompletedTask;
}
