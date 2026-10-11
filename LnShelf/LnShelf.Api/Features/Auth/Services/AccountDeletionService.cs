namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Deletes the current user and everything they own: series, volumes, user state, imports,
/// sync runs, preferences, the covers that are theirs alone and their avatar (feature-spec §7).
/// </summary>
// The rows go by cascade from the user row, in one SaveChanges. The cover files are deleted
// after that, best effort: a file left behind is unreachable (the covers endpoint checks the
// volume's owner), where a row left behind would still be someone's data. Series art keyed by
// a source id (anilist/…) is public catalogue art shared by every copy of that series, so it
// stays.
public sealed class AccountDeletionService(
    LnShelfDbContext dbContext,
    ICurrentUser currentUser,
    ICoverStore coverStore,
    ILogger<AccountDeletionService> logger)
{
    public async Task DeleteCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        // Under the owner filter, so these are this user's volumes and nobody else's.
        var volumeCovers = await dbContext.Volumes
            .Select(volume => new { volume.CoverObjectKey, Override = volume.UserState!.CoverOverrideObjectKey })
            .ToListAsync(cancellationToken);

        var keys = volumeCovers
            .SelectMany(cover => new[] { cover.CoverObjectKey, cover.Override })
            .OfType<string>()
            .Where(key => CoverObjectKey.VolumeIdOf(key) is not null)
            .ToList();

        // The avatar is the user's alone, whether copied from the provider or uploaded.
        var avatars = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => new { user.ProviderAvatarObjectKey, user.AvatarOverrideObjectKey })
            .SingleOrDefaultAsync(cancellationToken);

        if (avatars is not null)
        {
            keys.AddRange(new[] { avatars.ProviderAvatarObjectKey, avatars.AvatarOverrideObjectKey }.OfType<string>());
        }

        keys = keys.Distinct(StringComparer.Ordinal).ToList();

        await dbContext.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);

        foreach (var key in keys)
        {
            try
            {
                await coverStore.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not delete cover {ObjectKey} of a deleted account", key);
            }
        }

        logger.LogInformation("Deleted user {UserId} and {ObjectCount} of their covers and avatars", userId, keys.Count);
    }
}
