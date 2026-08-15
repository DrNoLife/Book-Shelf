namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The search endpoint's read path: ask the seam, cache the covers, map to the wire.
/// </summary>
// The cover caching lives here rather than in the seam because it is a presentation
// concern — a sync job matching titles has no use for the bytes. It is affordable
// because the search itself is cached for an hour, so a given query fetches its
// covers once and the page is then served entirely from local storage.
//
// Which source answered is decided one layer down, in ISeriesSearchService, and not here:
// `enrichment-verify` needs the same decision and has no use for cover bytes or wire
// contracts, so a copy of the rule in this class would be a copy that drifts.
public sealed class SeriesSearchPresentationService(
    ISeriesSearchService searchService,
    ISeriesCoverCache coverCache)
{
    public async Task<SeriesSearchResponse> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        var outcome = await searchService.SearchAsync(query, format, cancellationToken);

        // Keyed on the source's own id, which is unique within one outcome: every hit in
        // it came from the same source.
        var coverObjectKeys = new Dictionary<long, string>(outcome.Results.Count);
        foreach (var result in outcome.Results)
        {
            var objectKey = await coverCache.EnsureCachedAsync(
                result.Source, result.SourceId, result.CoverImageUrl, cancellationToken);

            if (objectKey is not null)
            {
                coverObjectKeys[result.SourceId] = objectKey;
            }
        }

        return new SeriesSearchResponse
        {
            Source = outcome.Source,
            Results = outcome.Results.ToResponses(coverObjectKeys),
            DegradedNote = outcome.DegradedNote,
        };
    }
}
