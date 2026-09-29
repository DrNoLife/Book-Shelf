namespace LnShelf.Api.Features.Releases.Services;

/// <inheritdoc />
public sealed class UpcomingQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : IUpcomingQueryService
{
    public async Task<UpcomingResponse> GetUpcomingAsync(Format? format, CancellationToken cancellationToken)
    {
        var query = dbContext.Series
            .AsNoTracking()
            .Include(series => series.UserState)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            // A dropped series is one the reader has said they are not buying, so its
            // releases are noise on a page that answers "what do I need to buy?". A
            // series with no user-state row has not been dropped.
            .Where(series => series.UserState == null || series.UserState.TrackingStatus != TrackingStatus.Dropped);

        if (format is not null)
        {
            query = query.Where(series => series.Format == format);
        }

        var series = await query.ToListAsync(cancellationToken);

        return UpcomingGrouping.Group(series, timeProvider.Today());
    }
}
