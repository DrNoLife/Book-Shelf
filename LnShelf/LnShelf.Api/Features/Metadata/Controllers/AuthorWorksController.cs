namespace LnShelf.Api.Features.Metadata.Controllers;

[ApiController]
[Route("api/series")]
public sealed class AuthorWorksController(IAuthorWorksQueryService authorWorksQueryService) : ControllerBase
{
    /// <summary>
    /// "More by {author}": the author's other works on AniList, most popular first, each
    /// marked when it is already on the shelf. Cached a day per series; the series page
    /// asks lazily, after it has drawn.
    /// </summary>
    // Rate-limited because a cache miss is an AniList request, but in its own bucket: every
    // series page asks, and flicking through the shelf must not starve search and add. A
    // source that will not serve surfaces as the central handler's 503, as search does.
    [EnableRateLimiting(UserRateLimits.AuthorWorks)]
    [HttpGet("{seriesId:guid}/more-by-author")]
    public async Task<ActionResult<AuthorWorksResponse>> GetAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var works = await authorWorksQueryService.GetForSeriesAsync(seriesId, cancellationToken);
        return works is null ? NotFound() : Ok(works);
    }
}
