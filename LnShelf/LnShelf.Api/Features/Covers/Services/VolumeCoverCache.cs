namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
public sealed class VolumeCoverCache(CoverFetcher fetcher) : IVolumeCoverCache
{
    public Task<string?> EnsureCachedAsync(
        Guid volumeId,
        string sourceName,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        // The synced prefix, never the override one. Sync cannot name an override's key,
        // which is what makes "overrides always win and sync never replaces one" a
        // structural guarantee rather than a rule someone has to remember.
        var objectKey = CoverObjectKey.ForVolumeSync(
            volumeId, sourceName, CoverObjectKey.ExtensionFor(sourceUri));

        return fetcher.EnsureCachedAsync(
            objectKey, sourceUri, $"volume {volumeId} from {sourceName}", cancellationToken);
    }
}
