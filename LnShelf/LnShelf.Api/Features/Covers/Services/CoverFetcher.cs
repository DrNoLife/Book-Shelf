namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Fetches one image and stores it under one key, once. The shared body of every
/// cover cache.
/// </summary>
// Extracted when the second cache arrived: the series cache and the per-volume cache
// differ only in how they name the key, and everything else — the exists check, the
// fetch, the validation, the deliberately broad catch — is one behaviour that must
// not drift into two copies.
public sealed class CoverFetcher(
    ICoverStore coverStore,
    CoverNormalizer normalizer,
    HttpClient httpClient,
    IOptions<CoverImageOptions> options,
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
            // Cached already: the whole point is one fetch per cover, ever. A refused fetch
            // below stores nothing, so it is asked again on the next sync — the same as a
            // failed one, and the reason a refusal returns null rather than throwing.
            if (await coverStore.ExistsAsync(objectKey, cancellationToken))
            {
                return objectKey;
            }

            // Headers first, so a declared size or type that is refused costs no body.
            using var response = await httpClient.GetAsync(
                sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await ReadImageAsync(response.Content, what, cancellationToken);
            if (content is null)
            {
                return null;
            }

            // Through the same normalizer as an upload, so what the store holds is always
            // this app's own webp: decoded by the narrowed decoders, size-capped, metadata
            // stripped. The key keeps the extension its source URL had — the store serves the
            // content type it was given, and renaming keys would orphan every cover cached
            // before this.
            var normalized = await normalizer.NormalizeAsync(content, cancellationToken);
            if (normalized is null)
            {
                logger.LogWarning("Refused the cover for {What}: {SourceUri} is not a usable image", what, sourceUri);
                return null;
            }

            await coverStore.SaveAsync(objectKey, normalized, CoverNormalizer.ContentType, cancellationToken);

            logger.LogInformation(
                "Cached the cover for {What} as {ObjectKey} ({ByteCount} bytes)",
                what,
                objectKey,
                normalized.Length);

            return objectKey;
        }
        // The caller's cancellation only. HttpClient.Timeout surfaces as a cancelled task
        // nobody asked for, and a CDN slow to hand over one image is exactly the hiccup the
        // catch below exists for.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Deliberately broad, and the one place in the codebase that is. A cover is
            // decoration: the shelf renders a placeholder without it, and failing an
            // import of 76 books — or a whole nightly sync — because the store or a CDN
            // hiccuped on one image would be the wrong trade. Everything else bubbles.
            logger.LogWarning(exception, "Could not cache the cover for {What}", what);
            return null;
        }
    }

    /// <summary>
    /// The body, when it says it is an image and fits under the size cap.
    /// </summary>
    /// <returns>Null when the response was refused; the reason is logged.</returns>
    // An absent Content-Type is let through to the normalizer, which decides by the bytes
    // anyway; a declared non-image one (an HTML error page served with a 200) is not.
    //
    // The cap is the upload cap: a synced cover and a dropped one end up as the same kind
    // of object, and a CDN has no better claim to a bigger one than the user does.
    private async Task<byte[]?> ReadImageAsync(HttpContent content, string what, CancellationToken cancellationToken)
    {
        var mediaType = content.Headers.ContentType?.MediaType;
        if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Refused the cover for {What}: the source sent {MediaType}", what, mediaType);
            return null;
        }

        var maximumBytes = options.Value.MaximumUploadBytes;
        if (content.Headers.ContentLength > maximumBytes)
        {
            logger.LogWarning(
                "Refused the cover for {What}: {ByteCount} bytes is over the {MaximumBytes} byte limit",
                what,
                content.Headers.ContentLength,
                maximumBytes);

            return null;
        }

        // Enforced while reading too: Content-Length is the sender's claim, and a chunked
        // response makes none at all.
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maximumBytes)
            {
                logger.LogWarning(
                    "Refused the cover for {What}: the body ran past the {MaximumBytes} byte limit",
                    what,
                    maximumBytes);

                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
