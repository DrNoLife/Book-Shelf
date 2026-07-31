namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// Caches one volume's synced art locally, so the shelf renders without the source's
/// CDN being up (CLAUDE.md — External metadata).
/// </summary>
// A sibling of ICoverCache rather than a second method on it. ICoverCache's signature
// is an AniList media id and one cover per series, which is the right shape for the
// series cover and cannot be bent to a volume without lying about what the id means.
// A new interface for a new shape keeps the existing one closed to modification.
public interface IVolumeCoverCache
{
    /// <param name="sourceName">
    /// Which source supplied the art. Part of the object key, so two sources' art for
    /// one volume cannot land on the same file.
    /// </param>
    /// <returns>
    /// The object key the cover endpoint serves it under, or null when caching failed —
    /// a cover is decoration, and the caller carries on without one.
    /// </returns>
    Task<string?> EnsureCachedAsync(
        Guid volumeId,
        string sourceName,
        Uri sourceUri,
        CancellationToken cancellationToken);
}
