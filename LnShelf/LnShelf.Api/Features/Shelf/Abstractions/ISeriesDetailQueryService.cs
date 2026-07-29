namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>Reads for the series-detail page.</summary>
public interface ISeriesDetailQueryService
{
    /// <returns>Null when there is no such series.</returns>
    Task<SeriesDetailResponse?> GetAsync(Guid seriesId, CancellationToken cancellationToken);
}

/// <summary>
/// Per-volume user-state writes: the read cycle, the owned toggle, and the bulk
/// "mark 1–N" an imported collection needs.
/// </summary>
public interface IVolumeCommandService
{
    /// <returns>Null when there is no such volume.</returns>
    Task<VolumeResponse?> SetReadAsync(
        Guid volumeId,
        SetVolumeReadRequest request,
        CancellationToken cancellationToken);

    /// <returns>Null when there is no such volume.</returns>
    Task<VolumeResponse?> SetOwnedAsync(
        Guid volumeId,
        SetVolumeOwnedRequest request,
        CancellationToken cancellationToken);

    /// <returns>The refreshed series, or null when there is no such series.</returns>
    Task<SeriesDetailResponse?> SetBulkAsync(
        Guid seriesId,
        BulkVolumeStateRequest request,
        CancellationToken cancellationToken);
}
