using LnShelf.Api.Features.Calendar.Abstractions;

namespace LnShelf.Api.Features.Calendar.Services;

/// <inheritdoc />
// The only read of calendar_feed by token. The table is not owner-filtered (CalendarFeed
// says why), so this needs no IgnoreQueryFilters. Not cached, so a turned-off or replaced
// link is dead from the very next fetch.
public sealed class CalendarFeedResolver(LnShelfDbContext dbContext) : ICalendarFeedResolver
{
    public async Task<Guid?> FindOwnerAsync(string token, CancellationToken cancellationToken)
    {
        if (!CalendarToken.IsWellFormed(token))
        {
            return null;
        }

        return await dbContext.CalendarFeeds
            .AsNoTracking()
            .Where(feed => feed.Token == token)
            .Select(feed => (Guid?)feed.UserId)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
