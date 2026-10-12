namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Where a series' tracking status should move to as the reader's volumes get read.
/// </summary>
// Pure on purpose: the volume writes, the add flow and the import all need the same answer,
// and three copies of it would drift on the details (what "out" means, what Completed needs).
public static class TrackingStatusRules
{
    /// <summary>
    /// The status the series should have now. Only ever forward: PlanToStart to Reading, and
    /// PlanToStart or Reading to Completed. Dropped and Completed are returned as they are,
    /// because a reader who dropped a series or marked it completed by hand decided that.
    /// </summary>
    /// <remarks>
    /// There is no automatic downgrade either: marking a volume unread on a Completed series
    /// leaves it Completed, and the reader can change it by hand.
    /// </remarks>
    public static TrackingStatus NextStatus(Series series, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);

        var current = series.UserState?.TrackingStatus ?? TrackingStatus.PlanToStart;
        if (current is not (TrackingStatus.PlanToStart or TrackingStatus.Reading))
        {
            return current;
        }

        if (IsFinishedAndFullyRead(series, today))
        {
            return TrackingStatus.Completed;
        }

        // An ongoing series with everything read is "caught up", not completed, so it lands
        // here like any series that has been started.
        return current is TrackingStatus.PlanToStart && series.Volumes.Any(IsStarted)
            ? TrackingStatus.Reading
            : current;
    }

    /// <summary>
    /// The status after the reader records a chapter: a PlanToStart series with a chapter
    /// above zero has been started, so it moves to Reading. Every other status stays.
    /// </summary>
    // Separate from NextStatus on purpose. Recording a chapter is evidence of starting and of
    // nothing else: running the volume rules from a chapter write would let it complete a
    // series the reader had set back to Reading by hand, which no chapter can justify.
    public static TrackingStatus AfterChapterRecorded(TrackingStatus current, decimal? lastChapterRead) =>
        current is TrackingStatus.PlanToStart && lastChapterRead > 0
            ? TrackingStatus.Reading
            : current;

    /// <summary>
    /// Whether a volume counts as published for the reader.
    /// </summary>
    // Mirrors the three grounds of SeriesDetailProjection.ToVolume's IsOut (and the copies
    // spelled out in LibraryCardProjection, which must stay SQL-translatable and so cannot
    // call this). TrackingStatusRulesTests pins this against ToVolume.
    public static bool IsOut(Volume volume, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return (volume.ReleaseDate is not null && volume.ReleaseDate <= today)
            || volume.IsReleased
            || volume.UserState?.Owned is true;
    }

    private static bool IsFinishedAndFullyRead(Series series, DateOnly today)
    {
        if (series.PublicationStatus is not PublicationStatus.Completed)
        {
            return false;
        }

        // Announced volumes do not block Completed, but a series with nothing out has
        // nothing to have finished.
        var outVolumes = series.Volumes.Where(volume => IsOut(volume, today)).ToList();

        return outVolumes.Count > 0
            && outVolumes.All(volume => volume.UserState?.ReadStatus is ReadStatus.Read);
    }

    private static bool IsStarted(Volume volume) =>
        volume.UserState?.ReadStatus is ReadStatus.Read or ReadStatus.Reading;
}
