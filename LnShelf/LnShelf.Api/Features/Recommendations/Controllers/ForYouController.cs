namespace LnShelf.Api.Features.Recommendations.Controllers;

[ApiController]
[Route("api/recommendations")]
public sealed class ForYouController(IForYouQueryService queryService) : ControllerBase
{
    /// <summary>The For you page: one "Because you read X" row per seed series.</summary>
    // Shares the series strip's bucket (see SeriesRecommendationsController); a source that
    // will not serve surfaces as the central handler's 503.
    [EnableRateLimiting(UserRateLimits.Recommendations)]
    [HttpGet]
    public async Task<ActionResult<ForYouResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await queryService.GetAsync(cancellationToken));
}
