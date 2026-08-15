namespace LnShelf.Api.Features.Metadata.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(SeriesSearchPresentationService searchService) : ControllerBase
{
    /// <summary>
    /// Searches the metadata source for series to add. Cached for an hour on
    /// query plus format (feature-spec §8), so typing does not spend the request
    /// budget twice on the same word.
    /// </summary>
    /// <param name="query">
    /// Bound from <c>q</c> — the query-string name feature-spec §6 fixes.
    /// </param>
    /// <param name="format">
    /// Omit to search every tracked format. The light novel and the manga of one
    /// title come back as two results either way.
    /// </param>
    // No try/catch: a source refusing to serve throws MetadataSourceUnavailableException,
    // which the central handler turns into a 503 naming the source and quoting it. The
    // substitution of one source for another happens below this, inside
    // ISeriesSearchService, so this method is unaware of it beyond passing on the note.
    [HttpGet]
    public async Task<ActionResult<SeriesSearchResponse>> SearchAsync(
        [FromQuery(Name = "q")] string query,
        [FromQuery] Format? format,
        CancellationToken cancellationToken)
    {
        return Ok(await searchService.SearchAsync(query, format, cancellationToken));
    }
}
