namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The search endpoint's read path: ask the seam, cache the covers, map to the wire.
/// </summary>
// The cover caching lives here rather than in the seam because it is a presentation
// concern — a sync job matching titles has no use for the bytes. It is affordable
// because the search itself is cached for an hour, so a given query fetches its
// covers once and the page is then served entirely from local storage.
public sealed class SeriesSearchPresentationService(
    ISeriesMetadataSource metadataSource,
    ICoverCache coverCache)
{
    public async Task<IReadOnlyList<SeriesSearchResultResponse>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        var results = await metadataSource.SearchAsync(query, format, cancellationToken);

        var coverObjectKeys = new Dictionary<int, string>(results.Count);
        foreach (var result in results)
        {
            var objectKey = await coverCache.EnsureCachedAsync(
                result.ExternalId, result.CoverImageUrl, cancellationToken);

            if (objectKey is not null)
            {
                coverObjectKeys[result.ExternalId] = objectKey;
            }
        }

        return results.ToResponses(coverObjectKeys);
    }
}
