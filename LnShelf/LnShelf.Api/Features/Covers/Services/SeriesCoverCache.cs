namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
public sealed class SeriesCoverCache(CoverFetcher fetcher, ILogger<SeriesCoverCache> logger) : ISeriesCoverCache
{
    public async Task<string?> EnsureCachedAsync(
        MetadataSourceName source,
        long sourceId,
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
                "{Source} {SourceId} carries an unusable cover URL {SourceUrl}",
                source,
                sourceId,
                sourceUrl);

            return null;
        }

        var objectKey = CoverObjectKey.ForSeriesSource(
            source, sourceId, CoverObjectKey.ExtensionFor(sourceUri));

        return await fetcher.EnsureCachedAsync(
            objectKey, sourceUri, $"{source} {sourceId}", cancellationToken);
    }
}
