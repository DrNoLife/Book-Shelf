using LnShelf.Api.Features.Activity.Abstractions;

namespace LnShelf.Api.Features.Activity.Services;

/// <inheritdoc />
public sealed class ActivityCommandService(
    LnShelfDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<ActivityCommandService> logger)
    : IActivityCommandService
{
    public async Task MarkSeenAsync(DateTimeOffset? through, CancellationToken cancellationToken)
    {
        // The panel's cursor rather than server time: a sync that writes news between the
        // panel loading and "Got it" must not have that news acknowledged unseen. Capped at
        // now so a wrong client clock cannot hide news that has not happened yet.
        var now = timeProvider.GetUtcNow();
        var seenAt = through is { } cursor && cursor < now ? cursor : now;

        var preferences = await dbContext.UserPreferences.FirstOrDefaultAsync(cancellationToken);

        // The preferences row is created on the first write, as PreferencesService does:
        // "Got it" is the reader's first opinion if they never touched a setting.
        if (preferences is null)
        {
            preferences = new UserPreferences();
            dbContext.UserPreferences.Add(preferences);
        }

        // Never backwards: a second tab dismissing an older panel must not bring back news
        // the reader already dismissed in the first.
        if (preferences.ActivitySeenAt is { } alreadySeen && alreadySeen > seenAt)
        {
            seenAt = alreadySeen;
        }

        preferences.ActivitySeenAt = seenAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Activity marked seen at {ActivitySeenAt}", preferences.ActivitySeenAt);
    }
}
