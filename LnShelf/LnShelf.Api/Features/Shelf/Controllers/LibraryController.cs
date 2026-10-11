namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/library")]
public sealed class LibraryController(ILibraryQueryService libraryQueryService) : ControllerBase
{
    /// <summary>Everything the library view renders, in one round trip.</summary>
    /// <param name="format">Omit for the "All" tab.</param>
    /// <param name="filters">
    /// Repeat it for several (<c>filter=Behind&amp;filter=Reading</c>); a series fitting any of
    /// them shows. Omitted, it is Behind, the opinionated default (§4.6), as it was when one
    /// value was all it took.
    /// </param>
    [HttpGet]
    public async Task<ActionResult<LibraryResponse>> GetLibraryAsync(
        [FromQuery] Format? format,
        [FromQuery(Name = "filter")] LibraryFilter[]? filters,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<LibraryFilter> chosen = filters is { Length: > 0 } ? filters : LibraryFilterSet.Default;

        var library = await libraryQueryService.GetLibraryAsync(format, chosen, cancellationToken);
        return Ok(library);
    }
}
