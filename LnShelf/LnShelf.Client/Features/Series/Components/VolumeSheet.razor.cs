namespace LnShelf.Client.Features.Series.Components;

public partial class VolumeSheet
{
    [Parameter]
    public required VolumeResponse Volume { get; set; }

    [Parameter]
    public required string SeriesTitle { get; set; }

    /// <summary>The source the reader picked for this volume's release date, or null for automatic.</summary>
    [Parameter]
    public MetadataSourceName? DateChoice { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public int CoverVersion { get; set; }

    [Parameter]
    public EventCallback Closed { get; set; }

    [Parameter]
    public EventCallback<VolumeResponse> CoverPickRequested { get; set; }

    [Parameter]
    public EventCallback<VolumeResponse> CommunityCoversRequested { get; set; }

    private VolumeTileState State => VolumeTileStateMap.For(Volume);

    /// <summary>"Owned, unread", "Not out yet · 12 Sep 2026" — the tile's state in full words.</summary>
    private string StateLine => State is VolumeTileState.Unreleased && Volume.ReleaseDate is { } releaseDate
        ? $"{VolumeTileStateMap.NameFor(State)} · {releaseDate.Full}"
        : VolumeTileStateMap.NameFor(State);

    // The first paragraph only, clamped: on a phone the sheet's job is the two choices, and a
    // long blurb would push them below the fold.
    private string? Description => Volume.Description?
        .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault();
}
