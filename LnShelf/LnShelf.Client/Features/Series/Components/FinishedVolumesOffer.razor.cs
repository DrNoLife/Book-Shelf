namespace LnShelf.Client.Features.Series.Components;

public partial class FinishedVolumesOffer
{
    /// <summary>A series counted in chapters; the offer reads its <c>VolumesFinishedByChapter</c>.</summary>
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    /// <summary>The chapter "Not now" was pressed at, so a later chapter brings the offer back.</summary>
    private decimal? _dismissedAtChapter;

    private bool _isDismissed;

    private List<decimal> VolumeNumbers => [.. Series.VolumesFinishedByChapter.Select(volume => volume.Number)];

    private bool IsShown =>
        Series.VolumesFinishedByChapter.Count > 0
        && !(_isDismissed && _dismissedAtChapter == Series.LastChapterRead);

    // The single-volume read write, once per offered volume: exactly these volumes, never the
    // "1–N" bulk run, which would also mark an earlier volume the reader skipped.
    private Task MarkReadAsync() =>
        SeriesState.MarkVolumesAsync(Series.VolumesFinishedByChapter, ReadStatus.Read, owned: null);

    private void NotNow()
    {
        _isDismissed = true;
        _dismissedAtChapter = Series.LastChapterRead;
    }
}
