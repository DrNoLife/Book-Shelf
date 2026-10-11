namespace LnShelf.Client.Features.Series;

/// <summary>
/// Which series and volumes the reader keeps by hand, the same rule the API enforces
/// (ManualSeriesCommandService): a series with neither external id, and the volumes on it
/// that no source supplied.
/// </summary>
public static class ManualSeries
{
    /// <summary>No metadata source knows it, so "Add volume" is offered.</summary>
    public static bool IsKeptByHand(SeriesDetailResponse series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return series.AniListId is null && series.MangaUpdatesId is null;
    }

    /// <summary>"Remove volume" is offered: added by hand, on a series nothing syncs.</summary>
    public static bool IsRemovable(SeriesDetailResponse series, VolumeResponse volume)
    {
        ArgumentNullException.ThrowIfNull(volume);
        return IsKeptByHand(series) && volume.IsLocalOnly;
    }
}
