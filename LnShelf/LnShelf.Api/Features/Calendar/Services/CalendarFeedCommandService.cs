using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Shared.Calendar;

namespace LnShelf.Api.Features.Calendar.Services;

/// <inheritdoc />
// The row is the setting, as with ShelfShareCommandService: present is on, absent is off.
// Off deletes it, so a link handed to a calendar app dies for good rather than waiting for a
// later "on" to bring it back.
//
// The token is never logged: it is the feed's whole access control.
public sealed class CalendarFeedCommandService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<CalendarFeedCommandService> logger) : ICalendarFeedCommandService
{
    public async Task<CalendarFeedResponse> SetOnAsync(bool isOn, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var feed = await dbContext.CalendarFeeds.FindAsync([userId], cancellationToken);

        if (isOn && feed is null)
        {
            feed = new CalendarFeed { UserId = userId, Token = CalendarToken.Create(), CreatedAt = timeProvider.GetUtcNow() };
            dbContext.CalendarFeeds.Add(feed);
        }
        else if (!isOn && feed is not null)
        {
            dbContext.CalendarFeeds.Remove(feed);
            feed = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Release calendar for {UserId} turned {State}", userId, isOn ? "on" : "off");

        return ToResponse(feed);
    }

    public async Task<CalendarFeedResponse> RegenerateAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var feed = await dbContext.CalendarFeeds.FindAsync([userId], cancellationToken)
                   ?? throw new ConflictException("Turn the calendar on first.");

        feed.Token = CalendarToken.Create();
        feed.CreatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Release calendar link for {UserId} regenerated", userId);

        return ToResponse(feed);
    }

    private static CalendarFeedResponse ToResponse(CalendarFeed? feed) =>
        new() { IsOn = feed is not null, Token = feed?.Token };
}
