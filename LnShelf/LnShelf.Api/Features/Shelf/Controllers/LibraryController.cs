namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/library")]
public sealed class LibraryController(ILibraryQueryService libraryQueryService) : ControllerBase
{
    /// <summary>Everything the library view renders, in one round trip.</summary>
    /// <param name="format">Omit for the "All" tab.</param>
    /// <param name="filter">Defaults to Behind — the opinionated default (§4.6).</param>
    [HttpGet]
    public async Task<ActionResult<LibraryResponse>> GetLibraryAsync(
        [FromQuery] Format? format,
        [FromQuery] LibraryFilter filter = LibraryFilter.Behind,
        CancellationToken cancellationToken = default)
    {
        var library = await libraryQueryService.GetLibraryAsync(format, filter, cancellationToken);
        return Ok(library);
    }
}
