namespace LnShelf.Api.Features.Shelf.Mappers;

/// <summary>
/// Metadata → a tracked series. Hand-written, explicit, no reflection
/// (CLAUDE.md — Mapping).
/// </summary>
public static class SeriesMetadataMapper
{
    /// <param name="syncedAt">
    /// Supplied by the caller from <c>TimeProvider</c>, the same way
    /// <see cref="LibraryCardProjection"/> takes "today".
    /// </param>
    /// <param name="coverObjectKey">
    /// The locally cached cover, or null when caching did not happen.
    /// </param>
    public static Series ToTrackedSeries(
        SeriesMetadata metadata,
        DateTimeOffset syncedAt,
        string? coverObjectKey)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = metadata.ExternalId,

            // MangaUpdates has not been consulted; phase 2's matching fills this.
            MangaUpdatesId = null,

            // The default user state: on the shelf, nothing recorded as read.
            // PlanToStart is also what the projection assumes for a series with
            // no row, so the two agree.
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.PlanToStart },
        };

        ApplyMetadata(series, metadata, syncedAt, coverObjectKey);

        foreach (var volume in metadata.Volumes)
        {
            series.Volumes.Add(ToVolume(volume));
        }

        return series;
    }

    /// <summary>
    /// Refreshes the metadata-owned fields of a series that already exists.
    /// </summary>
    // Nothing here touches SeriesUserState or VolumeUserState: local Postgres owns
    // user state and metadata never writes it (CLAUDE.md — External metadata). A
    // cover key of null leaves the existing one alone rather than clearing it,
    // because a failed fetch is not evidence that the cover is gone.
    public static void ApplyMetadata(
        Series series,
        SeriesMetadata metadata,
        DateTimeOffset syncedAt,
        string? coverObjectKey)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(metadata);

        series.Format = metadata.Format;
        series.TitleEnglish = metadata.TitleEnglish;
        series.TitleRomaji = metadata.TitleRomaji;
        series.TitleNative = metadata.TitleNative;
        series.PublicationStatus = metadata.PublicationStatus;
        series.Publisher = metadata.Publisher;
        series.StartYear = metadata.StartYear;
        series.Genres = [.. metadata.Genres];
        series.Author = metadata.Author;
        series.Illustrator = metadata.Illustrator;
        series.LastSyncedAt = syncedAt;

        if (coverObjectKey is not null)
        {
            series.CoverObjectKey = coverObjectKey;
        }
    }

    public static Volume ToVolume(VolumeMetadata volume)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = volume.Number,
            ReleaseDate = volume.ReleaseDate,
            ExternalId = volume.ExternalId,

            // Not local-only: the user did not create this volume, sync did, so a
            // later sync owns it and may correct it. IsLocalOnly is the flag that
            // protects a hand-added volume from exactly that.
            IsLocalOnly = false,
        };
    }
}
