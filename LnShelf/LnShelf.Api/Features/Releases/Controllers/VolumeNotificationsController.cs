namespace LnShelf.Api.Features.Releases.Controllers;

/// <summary>Per-volume "notify me when dated", on the Upcoming page's Announced group.</summary>
[ApiController]
[Route("api/volumes")]
public sealed class VolumeNotificationsController(
    IVolumeNotificationCommandService volumeNotificationCommandService) : ControllerBase
{
    /// <summary>Sets or clears the request. 204: the Upcoming page re-reads itself.</summary>
    [HttpPut("{volumeId:guid}/notify-when-dated")]
    public async Task<IActionResult> SetNotifyWhenDatedAsync(
        Guid volumeId,
        [FromBody] SetVolumeNotifyWhenDatedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await volumeNotificationCommandService.SetNotifyWhenDatedAsync(volumeId, request.NotifyWhenDated, cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
