namespace LnShelf.Api.Features.Releases.Services;

/// <inheritdoc />
// A write of its own rather than another field on PUT /state: muting is about notifications,
// which is this slice's concern, and the state endpoint's partial-update rules (null leaves a
// field alone) would have to grow a third meaning for a plain on/off.
//
// Mute is about pushes only. A muted series still records its events and still shows in
// "Since your last visit"; the notification pass is what skips it.
public sealed class SeriesNotificationCommandService(
    LnShelfDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SeriesNotificationCommandService> logger) : ISeriesNotificationCommandService
{
    public async Task<SeriesDetailResponse?> SetMutedAsync(Guid seriesId, bool muted, CancellationToken cancellationToken)
    {
        // The same graph SeriesDetailProjection.ToDetail reads, as every series write loads it,
        // so the answer is the whole refreshed series.
        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        (series.UserState ??= SeriesUserState.Untouched(series.Id)).NotificationsMuted = muted;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Notifications for series {SeriesId} {MuteState}", seriesId, muted ? "muted" : "unmuted");

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }
}
