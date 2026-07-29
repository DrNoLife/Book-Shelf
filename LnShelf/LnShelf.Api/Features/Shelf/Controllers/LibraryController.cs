namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/library")]
public sealed class LibraryController(ILibraryQueryService libraryQueryService) : ControllerBase
{
    /// <summary>Every card the library grid renders, in one round trip.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LibraryCardResponse>>> GetLibraryAsync(
        CancellationToken cancellationToken)
    {
        var cards = await libraryQueryService.GetLibraryAsync(cancellationToken);
        return Ok(cards);
    }
}
