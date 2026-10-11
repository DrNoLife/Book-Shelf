using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Shared.Calendar;

namespace LnShelf.Api.Features.Calendar.Controllers;

/// <summary>
/// The signed-in user's release calendar setting (feature-spec §4.9).
/// </summary>
[ApiController]
[Route("api/calendar")]
public sealed class CalendarFeedController(
    ICalendarFeedQueryService feedQuery,
    ICalendarFeedCommandService feedCommand) : ControllerBase
{
    /// <summary>On or off, and the feed's token while on.</summary>
    [HttpGet]
    public async Task<ActionResult<CalendarFeedResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await feedQuery.GetAsync(cancellationToken));

    /// <summary>On issues a link (or keeps the current one); off deletes it.</summary>
    [HttpPut]
    public async Task<ActionResult<CalendarFeedResponse>> UpdateAsync(
        [FromBody] UpdateCalendarFeedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Ok(await feedCommand.SetOnAsync(request.IsOn, cancellationToken));
    }

    /// <summary>A new link; the old one is a 404 from the next fetch on. 409 while off.</summary>
    [HttpPost("regenerate")]
    public async Task<ActionResult<CalendarFeedResponse>> RegenerateAsync(CancellationToken cancellationToken) =>
        Ok(await feedCommand.RegenerateAsync(cancellationToken));
}
