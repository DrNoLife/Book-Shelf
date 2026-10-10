namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesActionBar
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    /// <summary>
    /// The lowest-numbered volume that is owned and not read — the same rule as the library's
    /// "Continue reading" (LibraryCardProjection.NextToReadNumber). Null when nothing waits.
    /// </summary>
    private VolumeResponse? NextToRead =>
        Series.Volumes
            .Where(volume => volume.Owned && volume.ReadStatus is not ReadStatus.Read)
            .MinBy(volume => volume.Number);

    private Task MarkReadAsync(VolumeResponse volume) => SeriesState.SetReadAsync(volume, ReadStatus.Read);
}
