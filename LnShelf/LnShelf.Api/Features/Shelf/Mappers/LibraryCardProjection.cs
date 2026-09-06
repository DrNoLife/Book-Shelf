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

            // design.md: the card shows the latest volume's cover. "Latest" here is the
            // highest-numbered volume that actually has art — not simply the highest —
            // because a series is commonly tracking volumes the enrichment pass has never
            // reached, and picking the last of those would leave the shelf's most complete
            // series showing placeholders.
            //
            // The three grounds are the same ones, in the same order, that
            // SeriesDetailProjection.ToVolume uses for a tile: a user-supplied cover always
            // wins and sync never replaces it (CLAUDE.md — Cover overrides are user data),
            // then the volume's own synced art, then the series' cover.
            //
            // Spelled out inside the expression rather than shared with that method, for
            // the reason IsOut is spelled out four times below: this has to survive
            // translation to SQL, and a helper call would not.
            //
            // design.md also offers the reader a latest/first setting. It is not built, and
            // the column that used to stand in for it was deleted rather than left dead —
            // it can come back with the control that needs it.
            CoverObjectKey = series.Volumes
                .Where(volume =>
                    (volume.UserState != null && volume.UserState.CoverOverrideObjectKey != null)
                    || volume.CoverObjectKey != null)
                .OrderByDescending(volume => volume.Number)
                .Select(volume =>
                    volume.UserState != null && volume.UserState.CoverOverrideObjectKey != null
                        ? volume.UserState.CoverOverrideObjectKey
                        : volume.CoverObjectKey)
                .FirstOrDefault() ?? series.CoverObjectKey,

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

            // The soonest date still ahead of us — the rows view's next-volume column
            // when there is nothing to buy. Null across the whole shelf today, because
            // no source publishes per-volume dates (backlog: the enrichment spike).
            NextReleaseDate = series.Volumes
                .Where(volume => volume.ReleaseDate != null && volume.ReleaseDate > today)
                .OrderBy(volume => volume.ReleaseDate)
                .Select(volume => volume.ReleaseDate)
                .FirstOrDefault(),

            // One row per volume, carrying facts rather than an appearance: the tick's
            // four kinds are computed on VolumeTickResponse itself, so the strip and the
            // card's counts are reading the same rules. Kept dumb here on purpose —
            // anything smarter than a comparison stops being translatable SQL.
            VolumeTicks = series.Volumes
                .OrderBy(volume => volume.Number)
                .Select(volume => new VolumeTickResponse
                {
                    Number = volume.Number,
                    IsOut = (volume.ReleaseDate != null && volume.ReleaseDate <= today)
                            || volume.IsReleased
                            || (volume.UserState != null && volume.UserState.Owned),
                    Owned = volume.UserState != null && volume.UserState.Owned,
                    ReadStatus = volume.UserState != null ? volume.UserState.ReadStatus : ReadStatus.Unread,
                })
                .ToList(),
        };
}
