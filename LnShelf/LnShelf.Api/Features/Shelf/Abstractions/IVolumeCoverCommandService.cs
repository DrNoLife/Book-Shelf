namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// A volume's cover override: an upload, a pick from the covers other users share, and
/// removal. Split from <see cref="IVolumeCommandService"/>, which keeps the read and owned state.
/// </summary>
public interface IVolumeCoverCommandService
{
    /// <summary>
    /// Stores a user-supplied cover for one volume. It is normalized on ingest and
    /// wins over synced art for good (feature-spec §4.3).
    /// </summary>
    /// <returns>The refreshed series, or null when there is no such volume.</returns>
    Task<SeriesDetailResponse?> SetCoverAsync(
        Guid volumeId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    /// <summary>
    /// Makes a cover another user shares this volume's own, as a copy: from then on it is an
    /// ordinary override.
    /// </summary>
    /// <returns>
    /// The refreshed series, or null when there is no such volume or nobody shares that cover for it.
    /// </returns>
    Task<SeriesDetailResponse?> PickCommunityAsync(
        Guid volumeId,
        string contentHash,
        CancellationToken cancellationToken);

    /// <summary>Drops the override, falling back to whatever sync provides.</summary>
    /// <returns>The refreshed series, or null when there is no such volume.</returns>
    Task<SeriesDetailResponse?> RemoveCoverAsync(Guid volumeId, CancellationToken cancellationToken);
}
