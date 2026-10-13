namespace LnShelf.Api.Features.Covers.Abstractions;

/// <summary>
/// Caches a series' cover art locally on first reference, whichever series-level source
/// published it, so the shelf renders without that source's CDN being up
/// (CLAUDE.md — External metadata).
/// </summary>
// Takes a long because a MangaUpdates id is eleven digits and does not fit in an int. The
// fetch, the URL validation and the key layout all exist exactly once, behind this seam.
public interface ISeriesCoverCache
{
    /// <param name="source">
    /// Which source published the art. Part of the object key, so two sources' covers for
    /// one series cannot land on the same file — and so an AniList key is byte-for-byte
    /// what it was before this seam existed.
    /// </param>
    /// <returns>
    /// The object key the cover endpoint serves it under, or null when there was nothing
    /// to cache or caching failed — a cover is decoration, and the caller carries on
    /// without one.
    /// </returns>
    Task<string?> EnsureCachedAsync(
        MetadataSourceName source,
        long sourceId,
        string? sourceUrl,
        CancellationToken cancellationToken);
}
