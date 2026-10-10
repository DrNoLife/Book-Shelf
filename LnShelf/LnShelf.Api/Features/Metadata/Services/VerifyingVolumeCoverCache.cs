namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// An <see cref="IVolumeCoverCache"/> that proves a volume's cover can be fetched and
/// stores nothing.
/// </summary>
// Private to the verification command and never registered in the container. The real
// cache writes to the cover store, and a verification run that filled it with
// throwaway art would be changing the very thing it is supposed to be observing.
//
// It answers with a key no store holds, which is enough: VolumeFactWriter only sets
// Volume.CoverObjectKey when the cache returns one, and that column is what the report
// counts as "covered". So a URL whose bytes do not decode costs the volume its tick,
// exactly as a real caching failure would.
internal sealed class VerifyingVolumeCoverCache(VerificationImageProbe probe) : IVolumeCoverCache
{
    private readonly List<VerifiedImage> _verified = [];

    /// <summary>Every cover this run actually downloaded and decoded.</summary>
    public IReadOnlyList<VerifiedImage> Verified => _verified;

    public async Task<string?> EnsureCachedAsync(
        Guid volumeId,
        string sourceName,
        Uri sourceUri,
        CancellationToken cancellationToken)
    {
        var image = await probe.ProbeAsync(sourceUri, cancellationToken);

        if (image is null)
        {
            return null;
        }

        _verified.Add(image);

        // A pseudo key, and pseudo on purpose: it names no bucket, no prefix the cover
        // endpoint serves, and nothing was written under it.
        return $"verify/{volumeId}";
    }
}
