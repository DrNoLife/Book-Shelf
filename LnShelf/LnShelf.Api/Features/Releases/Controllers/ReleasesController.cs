namespace LnShelf.Api.Features.Releases.Controllers;

[ApiController]
[Route("api/releases")]
public sealed class ReleasesController(IUpcomingQueryService upcomingQueryService) : ControllerBase
{
    /// <summary>Out now and not owned, then what is dated, then what is announced.</summary>
    /// <param name="format">Omit for every format.</param>
    [HttpGet("upcoming")]
    public async Task<ActionResult<UpcomingResponse>> GetUpcomingAsync(
        [FromQuery] Format? format,
        CancellationToken cancellationToken = default)
    {
        var upcoming = await upcomingQueryService.GetUpcomingAsync(format, cancellationToken);
        return Ok(upcoming);
    }
}
