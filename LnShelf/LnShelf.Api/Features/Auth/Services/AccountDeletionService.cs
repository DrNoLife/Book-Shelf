namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Deletes the current user and everything they own: series, volumes, user state, imports,
/// sync runs, preferences, and the covers that are theirs alone (feature-spec §7).
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
            .Distinct(StringComparer.Ordinal)
            .ToList();

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

        logger.LogInformation("Deleted user {UserId} and {CoverCount} of their covers", userId, keys.Count);
    }
}
