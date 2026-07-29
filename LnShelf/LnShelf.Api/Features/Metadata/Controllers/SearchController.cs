namespace LnShelf.Api.Features.Metadata.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(ISeriesMetadataSource metadataSource) : ControllerBase
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
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeriesSearchResultResponse>>> SearchAsync(
        [FromQuery(Name = "q")] string query,
        [FromQuery] Format? format,
        CancellationToken cancellationToken)
    {
        var results = await metadataSource.SearchAsync(query, format, cancellationToken);
        return Ok(results.ToResponses());
    }
}
