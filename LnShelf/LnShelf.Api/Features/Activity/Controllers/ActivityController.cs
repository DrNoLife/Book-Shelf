using LnShelf.Api.Features.Activity.Abstractions;
using LnShelf.Shared.Activity;

namespace LnShelf.Api.Features.Activity.Controllers;

[ApiController]
[Route("api/activity")]
public sealed class ActivityController(
    IActivityQueryService activityQueryService,
    IActivityCommandService activityCommandService)
    : ControllerBase
{
    /// <summary>"Since your last visit": what the sync noticed, grouped per series.</summary>
    [HttpGet]
    public async Task<ActionResult<ActivityResponse>> GetActivityAsync(CancellationToken cancellationToken = default) =>
        Ok(await activityQueryService.GetActivityAsync(cancellationToken));

    /// <summary>"Got it": everything the dismissed panel read counts as seen.</summary>
    [HttpPost("seen")]
    public async Task<IActionResult> MarkSeenAsync(
        MarkActivitySeenRequest request,
        CancellationToken cancellationToken = default)
    {
        await activityCommandService.MarkSeenAsync(request.Through, cancellationToken);
        return NoContent();
    }
}
