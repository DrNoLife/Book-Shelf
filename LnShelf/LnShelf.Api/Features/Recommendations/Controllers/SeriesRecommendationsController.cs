namespace LnShelf.Api.Features.Recommendations.Controllers;

[ApiController]
[Route("api/series")]
public sealed class SeriesRecommendationsController(ISeriesRecommendationsQueryService queryService) : ControllerBase
{
    /// <summary>
    /// "Readers also liked": what readers of this series liked next, most voted first, with
    /// what is already on the shelf or hidden left out.
    /// </summary>
    // Rate-limited because a cache miss is an AniList request, but in its own bucket shared
    // with For you: every series page asks, and flicking through the shelf must not starve
    // search and add. A source that will not serve surfaces as the central handler's 503, as
    // search does.
    [EnableRateLimiting(UserRateLimits.Recommendations)]
    [HttpGet("{seriesId:guid}/recommendations")]
    public async Task<ActionResult<SeriesRecommendationsResponse>> GetAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var response = await queryService.GetForSeriesAsync(seriesId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
