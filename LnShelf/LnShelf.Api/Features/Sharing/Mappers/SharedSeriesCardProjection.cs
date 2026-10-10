namespace LnShelf.Api.Features.Sharing.Mappers;

/// <summary>
/// Turns a series row into a shared-shelf card, and nothing more.
/// </summary>
// An Expression for the same reason as LibraryCardProjection: one SQL statement, and the
// tests compile the very same expression. Its own projection rather than a trimmed library
// card, so a field the library carries (ownership, ids, dates) cannot reach a visitor by
// someone forgetting to blank it: what is not written here is not in the response.
public static class SharedSeriesCardProjection
{
    /// <summary>Dropped series are not on a shared shelf. Applied before <see cref="ToCard"/>.</summary>
    public static Expression<Func<Series, bool>> IsVisible =>
        series => series.UserState == null || series.UserState.TrackingStatus != TrackingStatus.Dropped;

    /// <param name="today">"Out" is relative to a day, so the caller supplies it.</param>
    public static Expression<Func<Series, SharedSeriesCardResponse>> ToCard(DateOnly today) =>
        series => new SharedSeriesCardResponse
        {
            TitleEnglish = series.TitleEnglish,
            TitleRomaji = series.TitleRomaji,
            Format = series.Format,
            PublicationStatus = series.PublicationStatus,

            // A series with no user-state row has not been shelved yet, as in the library.
            TrackingStatus = series.UserState != null
                ? series.UserState.TrackingStatus
                : TrackingStatus.PlanToStart,

            // Series art only: volume art and the owner's uploads stay off the public page,
            // so the anonymous cover endpoint never has to reason about volumes.
            CoverObjectKey = series.CoverObjectKey,

            Read = series.Volumes.Count(volume =>
                volume.UserState != null && volume.UserState.ReadStatus == ReadStatus.Read),

            // The library's rule with "read" where it says "owned": owning a volume proves it
            // is out, but counting it here would let the out count give away what the owner
            // has bought. Reading proves it just as well and the card already shows reads.
            Out = series.Volumes.Count(volume =>
                (volume.ReleaseDate != null && volume.ReleaseDate <= today)
                || volume.IsReleased
                || (volume.UserState != null && volume.UserState.ReadStatus == ReadStatus.Read)),
        };
}
