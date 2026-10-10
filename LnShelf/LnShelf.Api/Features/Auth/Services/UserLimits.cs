namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Refuses a write that would take the current user past their share (UserLimitOptions).
/// </summary>
// Counts run under the owner filters, so they are this user's counts and nobody else's.
// A refusal is a 409 with a sentence, like every other "not in this state" in the API.
public sealed class UserLimits(LnShelfDbContext dbContext, IOptions<UserLimitOptions> options)
{
    /// <summary>Throws when adding <paramref name="adding"/> series would pass the limit.</summary>
    public async Task EnsureRoomForSeriesAsync(int adding, CancellationToken cancellationToken)
    {
        var maximum = options.Value.MaximumSeriesPerUser;
        var tracked = await dbContext.Series.CountAsync(cancellationToken);

        if (tracked + adding > maximum)
        {
            throw new ConflictException(
                $"A shelf holds at most {maximum} series, and this one has {tracked}. Remove some to add more.");
        }
    }

    /// <summary>Throws when one more uploaded cover would pass the limit.</summary>
    public async Task EnsureRoomForCoverOverrideAsync(CancellationToken cancellationToken)
    {
        var maximum = options.Value.MaximumCoverOverridesPerUser;
        var uploaded = await dbContext.Volumes
            .CountAsync(volume => volume.UserState != null && volume.UserState.CoverOverrideObjectKey != null, cancellationToken);

        if (uploaded >= maximum)
        {
            throw new ConflictException(
                $"A shelf holds at most {maximum} uploaded covers. Remove one to upload another.");
        }
    }
}
