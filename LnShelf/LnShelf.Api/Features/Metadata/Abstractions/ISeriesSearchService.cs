namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Series search for the add flow, from whichever source can answer it.
/// </summary>
// The seam that exists so the rule "try AniList, fall back to MangaUpdates" has exactly
// one home. Two callers need it — the search endpoint and `enrichment-verify`, whose
// release gate would otherwise stay dead through an outage the app now survives — and a
// second copy of the rule in either of them is how the two would come to disagree about
// what counts as an outage.
public interface ISeriesSearchService
{
    /// <param name="format">Null searches every tracked format.</param>
    /// <exception cref="LnShelf.Api.Errors.MetadataSourceUnavailableException">
    /// No source could answer.
    /// </exception>
    Task<SeriesSearchOutcome> SearchAsync(string query, Format? format, CancellationToken cancellationToken);
}
