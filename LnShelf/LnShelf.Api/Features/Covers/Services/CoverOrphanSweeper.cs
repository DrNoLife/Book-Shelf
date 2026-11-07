namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Deletes uploaded covers no shelf points at (their volume is gone, or the volume names
/// another upload), and avatars no account points at.
/// </summary>
// Uploads and avatars are user data (GDPR Art. 5(1)(e), 17), and until series deletion started
// removing them uploads outlived the volume that named them, out of reach of the user and of
// account deletion. This clears what leaked, and what a best-effort delete since then failed
// to remove. A replaced cover whose delete failed, or a new upload whose save failed, sits under
// a volume that is still alive, so the volume alone cannot tell it is an orphan: the override
// keys the shelves name decide that. Only override/volume/ and avatar/ are swept: synced art
// (volume/...) is not user data.
public sealed class CoverOrphanSweeper(
    IServiceScopeFactory scopeFactory,
    ICoverStore coverStore,
    TimeProvider timeProvider,
    ILogger<CoverOrphanSweeper> logger)
{
    private const string UploadPrefix = "override/volume/";

    /// <summary>
    /// How old an unreferenced upload on a live volume must be before it counts as an orphan.
    /// </summary>
    // An upload is written before the row that names it is saved, and the sweep reads the keys
    // before the rows, so an upload whose row is saved between the two reads looks unreferenced.
    // That window is milliseconds; an hour leaves no doubt and still clears every real leftover.
    public static readonly TimeSpan UnreferencedUploadGracePeriod = TimeSpan.FromHours(1);

    public async Task<CoverOrphanSweepReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        // The keys first, the rows second. An upload is written after its volume was saved, so
        // the volume is in the later read unless it really is gone. The other order would miss
        // a volume added between the two reads and delete its fresh upload. Whether the volume
        // still names the upload has no such ordering to lean on (the file comes before the row
        // that names it), which is what UnreferencedUploadGracePeriod is for.
        //
        // An avatar is written just before the user row that names it, so a sweep landing in
        // those few milliseconds would take a brand-new avatar. Accepted: the sweep is a manual
        // one-shot, and the loss heals itself (the provider copy is fetched again at the next
        // sign-in, an upload is one click to repeat).
        var uploads = await ListParsedAsync(UploadPrefix, CoverObjectKey.VolumeIdOf, cancellationToken);
        var avatars = await ListParsedAsync(CoverObjectKey.AvatarPrefix, CoverObjectKey.AvatarUserIdOf, cancellationToken);

        // Every user's live volumes and the uploads they name, each read through the normal
        // owner-filtered context acting as that user.
        var liveUploads = await LiveUploadsOfEveryUserAsync(cancellationToken);
        var liveAvatarKeys = await LiveAvatarKeysAsync(cancellationToken);

        var orphanedUploads = await OrphanedUploadsAsync(uploads.Keys, liveUploads, cancellationToken);
        var orphans = orphanedUploads
            .Concat(avatars.Keys.Where(avatar => !liveAvatarKeys.Contains(avatar.Key)).Select(avatar => avatar.Key))
            .ToList();

        var (deleted, failed) = dryRun ? (0, 0) : await DeleteAsync(orphans, cancellationToken);
        var scanned = uploads.Scanned + avatars.Scanned;

        logger.LogInformation(
            "Cover orphan sweep (dry run: {DryRun}): {Scanned} scanned, {Orphaned} orphaned, {Deleted} deleted, {Failed} failed",
            dryRun, scanned, orphans.Count, deleted, failed);

        return new CoverOrphanSweepReport(scanned, orphans.Count, deleted, failed);
    }

    /// <summary>Every key under a prefix, with the owner it names; keys that name none are skipped.</summary>
    private async Task<ParsedKeys> ListParsedAsync(
        string prefix,
        Func<string, Guid?> ownerOf,
        CancellationToken cancellationToken)
    {
        var scanned = 0;
        var keys = new List<(string Key, Guid OwnerId)>();

        await foreach (var key in coverStore.ListKeysAsync(prefix, cancellationToken))
        {
            scanned++;

            // A key that does not parse is not this command's to judge.
            if (ownerOf(key) is { } ownerId)
            {
                keys.Add((key, ownerId));
            }
        }

        return new ParsedKeys(scanned, keys);
    }

    private async Task<IReadOnlyList<string>> OrphanedUploadsAsync(
        IReadOnlyList<(string Key, Guid OwnerId)> uploads,
        LiveUploads liveUploads,
        CancellationToken cancellationToken)
    {
        var orphans = new List<string>();
        var writtenBefore = timeProvider.GetUtcNow() - UnreferencedUploadGracePeriod;

        foreach (var (key, volumeId) in uploads)
        {
            if (!liveUploads.VolumeIds.Contains(volumeId))
            {
                orphans.Add(key);
            }
            else if (!liveUploads.ObjectKeys.Contains(key)
                     && await coverStore.GetLastModifiedAsync(key, cancellationToken) is { } lastModified
                     && lastModified < writtenBefore)
            {
                // Unreferenced on a live volume, and too old for a save naming it to still be
                // on its way. A file gone since the listing has no time and is left alone.
                orphans.Add(key);
            }
        }

        return orphans;
    }

    private async Task<(int Deleted, int Failed)> DeleteAsync(IReadOnlyList<string> orphans, CancellationToken cancellationToken)
    {
        var deleted = 0;
        var failed = 0;

        foreach (var key in orphans)
        {
            try
            {
                await coverStore.DeleteAsync(key, cancellationToken);
                deleted++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(exception, "Could not delete the orphaned cover {ObjectKey}", key);
            }
        }

        return (deleted, failed);
    }

    private async Task<LiveUploads> LiveUploadsOfEveryUserAsync(CancellationToken cancellationToken)
    {
        var volumeIds = new HashSet<Guid>();
        var objectKeys = await PublishedObjectKeysAsync(cancellationToken);

        foreach (var userId in await UserIdsAsync(cancellationToken))
        {
            // A scope per user: CurrentUser is scoped, and ActAs sticks for the whole scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);
            var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

            var volumes = await dbContext.Volumes
                .Select(volume => new
                {
                    volume.Id,
                    OverrideObjectKey = volume.UserState == null ? null : volume.UserState.CoverOverrideObjectKey,
                })
                .ToListAsync(cancellationToken);

            volumeIds.UnionWith(volumes.Select(volume => volume.Id));
            objectKeys.UnionWith(volumes.Select(volume => volume.OverrideObjectKey).OfType<string>());
        }

        return new LiveUploads(volumeIds, objectKeys);
    }

    // A published community cover names its sharer's override, which that volume's user state
    // names too. Counted on its own all the same: deleting a file another user can still pick
    // would break the pick, and this sweep deletes user data, so it errs towards keeping.
    private async Task<HashSet<string>> PublishedObjectKeysAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        var keys = await dbContext.CommunityCovers.Select(cover => cover.ObjectKey).ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    // The user table is unfiltered by design, so it can say whose shelves there are.
    private async Task<IReadOnlyList<Guid>> UserIdsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        return await dbContext.Users.Select(user => user.Id).ToListAsync(cancellationToken);
    }

    // By key, not by user: a user who still exists can own an orphaned avatar too (a replaced
    // picture whose delete failed).
    private async Task<HashSet<string>> LiveAvatarKeysAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        var references = await dbContext.Users
            .Select(user => new { user.ProviderAvatarObjectKey, user.AvatarOverrideObjectKey })
            .ToListAsync(cancellationToken);

        return references
            .SelectMany(reference => new[] { reference.ProviderAvatarObjectKey, reference.AvatarOverrideObjectKey })
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <param name="Scanned">Every key listed, parsed or not.</param>
    private sealed record ParsedKeys(int Scanned, IReadOnlyList<(string Key, Guid OwnerId)> Keys);

    /// <param name="VolumeIds">Every volume on any shelf.</param>
    /// <param name="ObjectKeys">Every upload a volume or a published community cover names.</param>
    private sealed record LiveUploads(IReadOnlySet<Guid> VolumeIds, IReadOnlySet<string> ObjectKeys);
}
