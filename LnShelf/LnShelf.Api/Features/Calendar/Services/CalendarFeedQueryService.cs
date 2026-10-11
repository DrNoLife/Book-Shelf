using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Shared.Calendar;

namespace LnShelf.Api.Features.Calendar.Services;

/// <inheritdoc />
// A primary-key lookup of the current user's own row, as ShelfShareQueryService does:
// calendar_feed is keyed by the user and has nothing else to look it up by. This is not
// hand-written owner filtering of shelf data.
public sealed class CalendarFeedQueryService(LnShelfDbContext dbContext, ICurrentUser currentUser)
    : ICalendarFeedQueryService
{
    public async Task<CalendarFeedResponse> GetAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var token = await dbContext.CalendarFeeds
            .AsNoTracking()
            .Where(feed => feed.UserId == userId)
            .Select(feed => feed.Token)
            .SingleOrDefaultAsync(cancellationToken);

        return new CalendarFeedResponse { IsOn = token is not null, Token = token };
    }
}
