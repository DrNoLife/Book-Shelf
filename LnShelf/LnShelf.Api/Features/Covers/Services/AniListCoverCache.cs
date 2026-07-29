namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
public sealed class AniListCoverCache(
    ICoverStore coverStore,
    HttpClient httpClient,
    ILogger<AniListCoverCache> logger) : ICoverCache
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

        try
        {
            // Cached already: the whole point is one fetch per cover, ever.
            if (await coverStore.ExistsAsync(objectKey, cancellationToken))
            {
                return objectKey;
            }

            using var response = await httpClient.GetAsync(sourceUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            await coverStore.SaveAsync(objectKey, content, contentType, cancellationToken);

            logger.LogInformation(
                "Cached the cover for AniList {AniListId} as {ObjectKey} ({ByteCount} bytes)",
                aniListId,
                objectKey,
                content.Length);

            return objectKey;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Deliberately broad, and the one place in the codebase that is. A cover
            // is decoration: the shelf renders a placeholder without it, and failing
            // an import of 76 books because MinIO or a CDN hiccuped on one image
            // would be the wrong trade. Everything else still bubbles.
            logger.LogWarning(
                exception, "Could not cache the cover for AniList {AniListId}", aniListId);
            return null;
        }
    }
}
