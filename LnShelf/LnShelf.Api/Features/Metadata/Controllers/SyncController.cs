namespace LnShelf.Api.Features.Metadata.Controllers;

[ApiController]
[Route("api/sync")]
public sealed class SyncController(
    ManualSyncRunner manualSyncRunner,
    ICurrentUser currentUser,
    ISyncQueryService syncQueryService) : ControllerBase
{
    /// <summary>
    /// Starts a sync now and returns without waiting for it. The same code path the nightly
    /// job uses (feature-spec §4.2 — "manual sync now … same code path").
    /// </summary>
    // 202 with no body: the run takes minutes, longer than the proxies in front of the API
    // will hold a request open. GET latest reports the last finished sweep, so the run shows
    // up in the sidebar once it ends (or as interrupted, if it stopped early).
    //
    // 409 rather than a quiet 202 when one is already going, so a caller is told the truth:
    // nothing new was started, and the run it is waiting for is the one already under way.
    [EnableRateLimiting(UserRateLimits.WholeShelfSync)]
    [HttpPost]
    public IActionResult Start()
    {
        if (!manualSyncRunner.TryStart(currentUser.Id))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "A sync is already running",
                Detail = "Your shelf is already being synced. The sidebar shows the result when it finishes.",
            });
        }

        return Accepted();
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
