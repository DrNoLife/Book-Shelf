using LnShelf.Api.Features.Calendar.Abstractions;

namespace LnShelf.Api.Features.Calendar.Controllers;

/// <summary>
/// The release calendar, for a calendar app subscribed to its link (feature-spec §4.9).
/// </summary>
// Anonymous on purpose: Google Calendar, Apple Calendar and Outlook fetch a subscribed feed
// from their own servers, with no session. Unknown, turned-off and malformed tokens all
// answer the same 404.
[ApiController]
[Route("api/calendar")]
[AllowAnonymous]
public sealed class ReleaseCalendarController(IReleaseCalendarQueryService releaseCalendar) : ControllerBase
{
    /// <summary>The feed as text/calendar.</summary>
    // Private for an hour: no shared cache (the Cloudflare edge) keeps it past a turned-off
    // link, and a calendar app polling every few minutes costs one query an hour per device.
    [HttpGet("{token}.ics")]
    [EnableRateLimiting(CalendarRateLimits.Feed)]
    public async Task<IActionResult> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (await releaseCalendar.GetAsync(token, cancellationToken) is not { } document)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=3600";

        return Content(document, "text/calendar; charset=utf-8");
    }
}
