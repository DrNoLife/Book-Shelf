namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// Caches a metadata source's cover art locally on first reference, so the shelf
/// renders without the AniList CDN being up (CLAUDE.md — External metadata).
/// </summary>
public interface ICoverCache
{
    /// <summary>
    /// Stores the image behind <paramref name="sourceUrl"/> if it is not stored
    /// already, and returns the object key the cover endpoint serves it under.
    /// </summary>
    /// <returns>
    /// Null when there was nothing to cache, or when caching failed — a cover is
    /// decoration, and the caller carries on without one.
    /// </returns>
    Task<string?> EnsureCachedAsync(int aniListId, string? sourceUrl, CancellationToken cancellationToken);
}
