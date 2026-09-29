namespace LnShelf.Api.Features.Stats.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(IStatsQueryService statsQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await statsQueryService.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }
}
