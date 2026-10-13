namespace LnShelf.Api.Features.Releases.Services;

/// <inheritdoc />
// The one exception to a series mute: a flagged volume's date event is pushed even when the
// series is muted, and the dispatcher clears the flag afterwards. On an unmuted series the
// digest already says it, so the client only offers the control where it changes something.
public sealed class VolumeNotificationCommandService(
    LnShelfDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<VolumeNotificationCommandService> logger) : IVolumeNotificationCommandService
{
    public async Task<bool> SetNotifyWhenDatedAsync(Guid volumeId, bool notifyWhenDated, CancellationToken cancellationToken)
    {
        // The owner filter reaches the volume through its series.
        var volume = await dbContext.Volumes
            .Include(candidate => candidate.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == volumeId, cancellationToken);

        if (volume is null)
        {
            return false;
        }

        if (notifyWhenDated && (volume.ReleaseDate is not null || TrackingStatusRules.IsOut(volume, timeProvider.Today())))
        {
            throw new ConflictException($"Vol {volume.Number.ToVolumeText()} already has a date; there is nothing to wait for.");
        }

        if (volume.UserState is null)
        {
            // Turning it off with no row is already the state asked for.
            if (!notifyWhenDated)
            {
                return true;
            }

            volume.UserState = new VolumeUserState { VolumeId = volume.Id };
        }

        volume.UserState.NotifyWhenDated = notifyWhenDated;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Notify-when-dated for volume {VolumeId} set to {NotifyWhenDated}", volumeId, notifyWhenDated);

        return true;
    }
}
