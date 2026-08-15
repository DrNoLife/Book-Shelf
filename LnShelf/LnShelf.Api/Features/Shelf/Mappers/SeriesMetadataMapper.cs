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
    /// A tracked series from MangaUpdates, for when AniList cannot be reached at all.
    /// </summary>
    /// <param name="coverObjectKey">
    /// The locally cached cover, or null when caching did not happen.
    /// </param>
    // Its own method rather than a second SeriesMetadata producer, because the two sources
    // do not describe a series the same way and this is where the difference lands:
    //
    // - The volumes come from a *count*, parsed out of prose ("9 Volumes + 1 Extra
    //   (Complete)"), and are marked released. That is what a release source's count means
    //   — the same claim SeriesRefresher.MarkReleased writes for a series linked later —
    //   and it is stronger than what AniList's volume list supports, which is why the
    //   AniList path leaves IsReleased alone.
    // - MangaUpdatesId is set and AniListId stays null, so the nightly refresh skips
    //   matching for this series entirely: it is already linked to the release source.
    public static Series ToTrackedSeries(
        MangaUpdatesSeriesMetadata metadata,
        DateTimeOffset syncedAt,
        string? coverObjectKey)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),

            // AniList was not consulted — it could not be. A later refresh does not go
            // looking for an AniList id either; nothing in the shelf matches back onto it.
            AniListId = null,
            MangaUpdatesId = metadata.SourceId,

            Format = metadata.Format,
            TitleEnglish = metadata.TitleEnglish,

            // The whole reason this column exists. MangaUpdates files this series under
            // whichever title its editors chose — often the romaji one — and the English
            // name the US storefront lists it under is one unlabelled entry among the
            // Russian and Arabic ones. Stored so the volume catalogue can be asked under
            // each in turn until one is recognised.
            AlternativeTitles = SeriesKnownTitles.Alternatives(
                metadata.TitleEnglish, metadata.AssociatedTitles),

            PublicationStatus = metadata.PublicationStatus,
            Publisher = metadata.Publisher,
            StartYear = metadata.StartYear,
            Genres = [.. metadata.Genres],
            Author = metadata.Author,
            Illustrator = metadata.Illustrator,
            Description = metadata.Description,
            CoverObjectKey = coverObjectKey,
            LastSyncedAt = syncedAt,

            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.PlanToStart },
        };

        for (var number = 1; number <= (metadata.VolumeCount ?? 0); number++)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // No date, because MangaUpdates has none to give — its API carries no
                // per-volume dates at all. The volume-enrichment pass fills them in.
                ReleaseDate = null,

                // The source says these volumes exist in print, which is exactly what
                // "to buy" needs to know.
                IsReleased = true,

                // Sync created it, so sync owns it.
                IsLocalOnly = false,
            });
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

        // Romaji only. TitleNative is deliberately left out: it is the Japanese title in
        // Japanese script, and the US storefront the catalogue pass searches cannot match a
        // word of it — storing it would buy nothing and cost a request per sweep.
        //
        // Overwritten rather than filled if empty, on the same rule as Genres: this is
        // metadata the source owns, so whatever it says now is the answer. A series added
        // before the column existed keeps an empty one until it is re-applied, which costs it
        // nothing — its stored title is already the English one AniList supplies, and that is
        // the first title the catalogue is asked under.
        series.AlternativeTitles = SeriesKnownTitles.Alternatives(
            metadata.TitleEnglish, [metadata.TitleRomaji]);

        series.PublicationStatus = metadata.PublicationStatus;
        series.Publisher = metadata.Publisher;
        series.StartYear = metadata.StartYear;
        series.Genres = [.. metadata.Genres];
        series.Author = metadata.Author;
        series.Illustrator = metadata.Illustrator;
        series.Description = metadata.Description;
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
