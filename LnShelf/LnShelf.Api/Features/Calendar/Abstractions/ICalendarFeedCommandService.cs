using LnShelf.Shared.Calendar;

namespace LnShelf.Api.Features.Calendar.Abstractions;

/// <summary>
/// Turns the signed-in user's release calendar on or off, or gives it a new link (feature-spec §4.9).
/// </summary>
public interface ICalendarFeedCommandService
{
    /// <summary>
    /// On issues a link (or keeps the current one); off deletes it, so turning it on again
    /// gives a new link and an old one never comes back.
    /// </summary>
    Task<CalendarFeedResponse> SetOnAsync(bool isOn, CancellationToken cancellationToken);

    /// <summary>Replaces the link. Throws <see cref="ConflictException"/> while the feed is off.</summary>
    Task<CalendarFeedResponse> RegenerateAsync(CancellationToken cancellationToken);
}
