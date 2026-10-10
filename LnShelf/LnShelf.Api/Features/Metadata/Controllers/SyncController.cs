namespace LnShelf.Api.Features.Metadata.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    ISeriesSyncService seriesSyncService,
    ISyncQueryService syncQueryService) : ControllerBase
{
    /// <summary>
    /// Runs a sync now. The same code path the nightly job uses (feature-spec §4.2
    /// — "manual sync now … same code path").
    /// </summary>
    [EnableRateLimiting(UserRateLimits.WholeShelfSync)]
    [HttpPost]
    public async Task<ActionResult<SyncSummaryResponse>> RunAsync(CancellationToken cancellationToken)
    {
        var run = await seriesSyncService.RunAsync(cancellationToken);
        return Ok(run.ToResponse());
    }

    /// <summary>
    /// The sidebar's summary and the list of everything the run left for a person.
    /// </summary>
    [HttpGet("latest")]
    public async Task<ActionResult<SyncSummaryResponse>> GetLatestAsync(CancellationToken cancellationToken)
    {
        var latest = await syncQueryService.GetLatestAsync(cancellationToken);

        // 204 rather than 404: "nothing has been synced yet" is a state the sidebar
        // renders, not a missing resource.
        return latest is null ? NoContent() : Ok(latest);
    }
}
