namespace LnShelf.Api.Features.Shelf.Mappers;

/// <summary>
/// Metadata → a newly tracked series. Hand-written, explicit, no reflection
/// (CLAUDE.md — Mapping).
/// </summary>
public static class SeriesMetadataMapper
{
    /// <param name="syncedAt">
    /// Supplied by the caller from <c>TimeProvider</c>, the same way
    /// <see cref="LibraryCardProjection"/> takes "today".
    /// </param>
    public static Series ToTrackedSeries(SeriesMetadata metadata, DateTimeOffset syncedAt)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = metadata.ExternalId,

            // MangaUpdates has not been consulted; phase 2's matching fills this.
            MangaUpdatesId = null,

            Format = metadata.Format,
            TitleEnglish = metadata.TitleEnglish,
            TitleRomaji = metadata.TitleRomaji,
            TitleNative = metadata.TitleNative,
            PublicationStatus = metadata.PublicationStatus,
            Publisher = metadata.Publisher,
            StartYear = metadata.StartYear,
            Genres = [.. metadata.Genres],
            Author = metadata.Author,
            Illustrator = metadata.Illustrator,

            // Adding *is* a metadata fetch, so the series is synced as of now.
            LastSyncedAt = syncedAt,

            // The default user state: on the shelf, nothing recorded as read.
            // PlanToStart is also what the projection assumes for a series with
            // no row, so the two agree.
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.PlanToStart },
        };

        foreach (var volume in metadata.Volumes)
        {
            series.Volumes.Add(ToVolume(volume));
        }

        return series;
    }

    private static Volume ToVolume(VolumeMetadata volume) => new()
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
