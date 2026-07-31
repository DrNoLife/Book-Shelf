namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
public sealed class AniListCoverCache(CoverFetcher fetcher, ILogger<AniListCoverCache> logger) : ICoverCache
{
    public async Task<string?> EnsureCachedAsync(
        int aniListId,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
        {
            logger.LogWarning(
                "AniList {AniListId} carries an unusable cover URL {SourceUrl}", aniListId, sourceUrl);
            return null;
        }

        var objectKey = CoverObjectKey.ForAniList(aniListId, CoverObjectKey.ExtensionFor(sourceUri));

        return await fetcher.EnsureCachedAsync(
            objectKey, sourceUri, $"AniList {aniListId}", cancellationToken);
    }
}
