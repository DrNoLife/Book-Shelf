namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
// An adapter onto ISeriesCoverCache rather than a second implementation of the same
// behaviour. The AniList-shaped seam stays because four callers legitimately have an
// AniList media id in hand and nothing else — the import matcher and the add flow among
// them — and it should not be their job to name a source they already know.
public sealed class AniListCoverCache(ISeriesCoverCache seriesCoverCache) : ICoverCache
{
    public Task<string?> EnsureCachedAsync(
        int aniListId,
        string? sourceUrl,
        CancellationToken cancellationToken) =>
        seriesCoverCache.EnsureCachedAsync(
            MetadataSourceName.AniList, aniListId, sourceUrl, cancellationToken);
}
