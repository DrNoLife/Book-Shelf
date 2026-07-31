namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Fetches one image and stores it under one key, once. The shared body of every
/// cover cache.
/// </summary>
// Extracted when the second cache arrived: the series cache and the per-volume cache
// differ only in how they name the key, and everything else — the exists check, the
// fetch, the content type, the deliberately broad catch — is one behaviour that must
// not drift into two copies.
public sealed class CoverFetcher(
    ICoverStore coverStore,
    HttpClient httpClient,
    ILogger<CoverFetcher> logger)
{
    /// <param name="what">What is being cached, for the log line only.</param>
    /// <returns>
    /// The object key, or null when there was nothing to cache or caching failed — a
    /// cover is decoration, and the caller carries on without one.
    /// </returns>
    public async Task<string?> EnsureCachedAsync(
        string objectKey,
        Uri sourceUri,
        string what,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(sourceUri);

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
                "Cached the cover for {What} as {ObjectKey} ({ByteCount} bytes)",
                what,
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
            // Deliberately broad, and the one place in the codebase that is. A cover is
            // decoration: the shelf renders a placeholder without it, and failing an
            // import of 76 books — or a whole nightly sync — because MinIO or a CDN
            // hiccuped on one image would be the wrong trade. Everything else bubbles.
            logger.LogWarning(exception, "Could not cache the cover for {What}", what);
            return null;
        }
    }
}
