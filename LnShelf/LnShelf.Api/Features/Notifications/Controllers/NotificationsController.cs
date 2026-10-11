using Microsoft.Net.Http.Headers;

namespace LnShelf.Api.Features.Notifications.Controllers;

/// <summary>
/// The profile's Notifications card: which devices get a push after a sync (feature-spec §4.5).
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(
    IPushSubscriptionQueryService subscriptionQuery,
    IPushSubscriptionCommandService subscriptionCommand) : ControllerBase
{
    /// <summary>Whether the server can push, the key to subscribe with, and the devices that are on.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await subscriptionQuery.GetAsync(cancellationToken));

    /// <summary>
    /// Turns notifications on for this browser, or refreshes its keys. 409 when the server has
    /// no Web Push keys; 400 for an endpoint that is not a known push service.
    /// </summary>
    // Rate limited like the calls that reach outside: each subscription is a place every later
    // pass sends to.
    [HttpPost("subscriptions")]
    [EnableRateLimiting(UserRateLimits.ExternalSources)]
    public async Task<ActionResult<PushDeviceResponse>> SubscribeAsync(
        [FromBody] CreatePushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userAgent = Request.Headers[HeaderNames.UserAgent].ToString();
        return Ok(await subscriptionCommand.SubscribeAsync(request, userAgent, cancellationToken));
    }

    /// <summary>Turns notifications off for one device.</summary>
    [HttpDelete("subscriptions/{subscriptionId:guid}")]
    public async Task<IActionResult> RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken) =>
        await subscriptionCommand.RemoveAsync(subscriptionId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Sends a test notification to every device. 409 when the server has no Web Push keys.</summary>
    [HttpPost("test")]
    [EnableRateLimiting(UserRateLimits.ExternalSources)]
    public async Task<ActionResult<PushTestResponse>> SendTestAsync(CancellationToken cancellationToken) =>
        Ok(await subscriptionCommand.SendTestAsync(cancellationToken));
}
