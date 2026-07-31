namespace LnShelf.Api.Features.Shelf.Mappers;

/// <summary>
/// Turns a loaded series into the detail response.
/// </summary>
// Not an Expression like LibraryCardProjection: the grid needs one row per volume
// rather than aggregate counts, so the series is loaded with its volumes and shaped
// in memory. The derived-state rules are not duplicated — "is this volume out" lives
// in one place here and the counts are summed from it, so a tile and the library card
// above it cannot disagree.
public static class SeriesDetailProjection
{
    public static SeriesDetailResponse ToDetail(Series series, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);

        var volumes = series.Volumes
            .OrderBy(volume => volume.Number)
            .Select(volume => ToVolume(volume, today))
            .ToList();

        return new SeriesDetailResponse
        {
            SeriesId = series.Id,
            TitleEnglish = series.TitleEnglish,
            TitleRomaji = series.TitleRomaji,
            TitleNative = series.TitleNative,
            Format = series.Format,
            PublicationStatus = series.PublicationStatus,
            TrackingStatus = series.UserState?.TrackingStatus ?? TrackingStatus.PlanToStart,
            Author = series.Author,
            Illustrator = series.Illustrator,
            Publisher = series.Publisher,
            StartYear = series.StartYear,
            Genres = [.. series.Genres],
            CoverObjectKey = series.CoverObjectKey,
            AniListId = series.AniListId,
            MangaUpdatesId = series.MangaUpdatesId,
            Notes = NullIfBlank(series.UserState?.Notes),
            StartedAt = series.UserState?.StartedAt,
            LastReadAt = series.UserState?.LastReadAt,
            HasNewVolume = series.UserState?.HasNewVolume ?? false,
            VolumeViewOverride = series.UserState?.VolumeViewOverride,
            Counts = ToCounts(volumes),
            Spend = ToSpend(volumes),
            Relation = null,
            Volumes = volumes,
        };
    }

    public static VolumeResponse ToVolume(Volume volume, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(volume);

        var state = volume.UserState;
        var owned = state?.Owned ?? false;

        return new VolumeResponse
        {
            VolumeId = volume.Id,
            Number = volume.Number,
            ReleaseDate = volume.ReleaseDate,

            // The same three grounds as the library card's Published.
            IsOut = (volume.ReleaseDate is not null && volume.ReleaseDate <= today)
                    || volume.IsReleased
                    || owned,

            ReadStatus = state?.ReadStatus ?? ReadStatus.Unread,
            Owned = owned,
            OwnedFormat = owned ? state?.OwnedFormat : null,
            ReadAt = state?.ReadAt,
            PurchasePrice = state?.PurchasePrice,

            // A user-supplied cover always wins over synced art, and sync never
            // replaces it (CLAUDE.md — Cover overrides are user data).
            CoverObjectKey = state?.CoverOverrideObjectKey ?? volume.CoverObjectKey,
            HasCoverOverride = state?.CoverOverrideObjectKey is not null,
            IsLocalOnly = volume.IsLocalOnly,
        };
    }

    private static SeriesCountsResponse ToCounts(List<VolumeResponse> volumes) => new()
    {
        Read = volumes.Count(volume => volume.ReadStatus is ReadStatus.Read),
        Owned = volumes.Count(volume => volume.Owned),
        Published = volumes.Count(volume => volume.IsOut),
        ToBuy = volumes.Count(volume => volume.IsOut && !volume.Owned),
        Backlog = volumes.Count(volume => volume.Owned && volume.ReadStatus is not ReadStatus.Read),
        Tracked = volumes.Count,
        NextToBuyNumber = volumes
            .Where(volume => volume.IsOut && !volume.Owned)
            .Select(volume => (int?)volume.Number)
            .FirstOrDefault(),
        NotOut = volumes.Count(volume => !volume.IsOut),
        Dated = volumes.Count(volume => !volume.IsOut && volume.ReleaseDate is not null),
    };

    /// <summary>
    /// The spend line, or nothing at all.
    /// </summary>
    // Averaged over the volumes that carry a price, not over the volumes owned:
    // dividing a partial total by the full count would quietly understate the average
    // of what is actually recorded.
    private static SeriesSpendResponse? ToSpend(List<VolumeResponse> volumes)
    {
        var priced = volumes.Where(volume => volume.PurchasePrice is > 0).ToList();
        if (priced.Count == 0)
        {
            return null;
        }

        var total = priced.Sum(volume => volume.PurchasePrice!.Value);

        return new SeriesSpendResponse
        {
            PricedCount = priced.Count,
            Total = total,
            Average = decimal.Round(total / priced.Count, 0, MidpointRounding.AwayFromZero),
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
