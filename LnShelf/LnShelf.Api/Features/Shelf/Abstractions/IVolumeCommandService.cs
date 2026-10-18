namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Per-volume user-state writes: the read cycle, the owned toggle, and the bulk
/// "mark 1–N" an imported collection needs. Cover overrides are <see cref="IVolumeCoverCommandService"/>.
/// </summary>
// Every write answers with the whole series rather than the one row it touched. A volume
// is never only itself — the hero's counts, the next-to-buy CTA and the progress bar are
// all derived from the set — and returning the row alone left the client re-deriving those
// counts from its own copy of the rule (CLAUDE.md: derived states are computed in queries).
public interface IVolumeCommandService
{
    /// <returns>The refreshed series, or null when there is no such volume.</returns>
    Task<SeriesDetailResponse?> SetReadAsync(
        Guid volumeId,
        SetVolumeReadRequest request,
        CancellationToken cancellationToken);

    /// <returns>The refreshed series, or null when there is no such volume.</returns>
    Task<SeriesDetailResponse?> SetOwnedAsync(
        Guid volumeId,
        SetVolumeOwnedRequest request,
        CancellationToken cancellationToken);

    /// <returns>The refreshed series, or null when there is no such series.</returns>
    Task<SeriesDetailResponse?> SetBulkAsync(
        Guid seriesId,
        BulkVolumeStateRequest request,
        CancellationToken cancellationToken);
}
