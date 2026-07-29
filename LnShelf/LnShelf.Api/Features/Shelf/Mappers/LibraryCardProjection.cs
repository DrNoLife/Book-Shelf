namespace LnShelf.Api.Features.Shelf.Mappers;

/// <summary>
/// Turns a series row into a library card.
/// </summary>
// Kept as an Expression so EF translates it into one SQL statement (the grid is
// the hot path — data-access.md) while the tests compile the very same
// expression and run it in memory. There is no second copy of the derived-state
// rules to drift out of sync.
public static class LibraryCardProjection
{
    /// <param name="today">
    /// "Released" is relative to a day, so the caller supplies it — production
    /// from TimeProvider, tests from a fixed date.
    /// </param>
    public static Expression<Func<Series, LibraryCardResponse>> ToCard(DateOnly today) =>
        series => new LibraryCardResponse
        {
            SeriesId = series.Id,
            TitleEnglish = series.TitleEnglish,
            TitleRomaji = series.TitleRomaji,
            Format = series.Format,
            PublicationStatus = series.PublicationStatus,

            // A series with no user-state row has not been shelved yet.
            TrackingStatus = series.UserState != null
                ? series.UserState.TrackingStatus
                : TrackingStatus.PlanToStart,
            HasNewVolume = series.UserState != null && series.UserState.HasNewVolume,

            // The series cover. design.md's "latest volume, or first by setting"
            // needs per-volume art to choose between, which arrives with
            // MangaUpdates in phase 2; until then there is exactly one cover.
            CoverObjectKey = series.CoverObjectKey,

            Read = series.Volumes.Count(volume =>
                volume.UserState != null && volume.UserState.ReadStatus == ReadStatus.Read),

            Owned = series.Volumes.Count(volume =>
                volume.UserState != null && volume.UserState.Owned),

            // Published on any of three grounds, because no single source is enough:
            //   a date in the past          — the ideal, and rare: no source has them
            //   the release source says so  — Volume.IsReleased, a sourced count
            //   the user owns it            — you cannot own an unreleased volume
            // Without the last two an imported library reports published=0 while
            // owning 74 volumes, and every progress bar spans zero.
            Published = series.Volumes.Count(volume =>
                (volume.ReleaseDate != null && volume.ReleaseDate <= today)
                || volume.IsReleased
                || (volume.UserState != null && volume.UserState.Owned)),

            // To buy = out and not owned. Ownership is the one ground for "published"
            // that cannot count here, by definition — which is why this repeats the
            // other two rather than reusing Published.
            ToBuy = series.Volumes.Count(volume =>
                ((volume.ReleaseDate != null && volume.ReleaseDate <= today) || volume.IsReleased)
                && (volume.UserState == null || !volume.UserState.Owned)),

            // Backlog = owned and not read. "Reading" still counts as backlog.
            Backlog = series.Volumes.Count(volume =>
                volume.UserState != null
                && volume.UserState.Owned
                && volume.UserState.ReadStatus != ReadStatus.Read),

            Tracked = series.Volumes.Count,

            LastReadAt = series.UserState != null ? series.UserState.LastReadAt : null,
            LastSyncedAt = series.LastSyncedAt,

            NextToBuyNumber = series.Volumes
                .Where(volume =>
                    ((volume.ReleaseDate != null && volume.ReleaseDate <= today) || volume.IsReleased)
                    && (volume.UserState == null || !volume.UserState.Owned))
                .OrderBy(volume => volume.Number)
                .Select(volume => (int?)volume.Number)
                .FirstOrDefault(),
        };
}
