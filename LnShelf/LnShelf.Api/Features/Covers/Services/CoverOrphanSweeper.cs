namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Deletes uploaded covers whose volume is gone from every shelf, and avatars no account
/// points at.
/// </summary>
// Uploads and avatars are user data (GDPR Art. 5(1)(e), 17), and until series deletion started
// removing them uploads outlived the volume that named them, out of reach of the user and of
// account deletion. This clears what leaked, and what a best-effort delete since then failed
// to remove. Only override/volume/ and avatar/ are swept: synced art (volume/...) is not
// user data.
public sealed class CoverOrphanSweeper(
    IServiceScopeFactory scopeFactory,
    ICoverStore coverStore,
    ILogger<CoverOrphanSweeper> logger)
{
    private const string UploadPrefix = "override/volume/";

    public async Task<CoverOrphanSweepReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        // The keys first, the rows second. An upload is written after its volume was saved, so
        // the volume is in the later read unless it really is gone. The other order would miss
        // a volume added between the two reads and delete its fresh upload.
        //
        // An avatar is written just before the user row that names it, so a sweep landing in
        // those few milliseconds would take a brand-new avatar. Accepted: the sweep is a manual
        // one-shot, and the loss heals itself (the provider copy is fetched again at the next
        // sign-in, an upload is one click to repeat).
        var uploads = await ListParsedAsync(UploadPrefix, CoverObjectKey.VolumeIdOf, cancellationToken);
        var avatars = await ListParsedAsync(CoverObjectKey.AvatarPrefix, CoverObjectKey.AvatarUserIdOf, cancellationToken);

        // A cover is orphaned when no shelf has its volume, so every user's live volumes are
        // collected, each through the normal owner-filtered context acting as that user.
        var liveVolumeIds = await LiveVolumeIdsOfEveryUserAsync(cancellationToken);
        var liveAvatarKeys = await LiveAvatarKeysAsync(cancellationToken);

        var orphans = uploads.Keys.Where(upload => !liveVolumeIds.Contains(upload.OwnerId))
            .Concat(avatars.Keys.Where(avatar => !liveAvatarKeys.Contains(avatar.Key)))
            .Select(orphan => orphan.Key)
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

    private async Task<HashSet<Guid>> LiveVolumeIdsOfEveryUserAsync(CancellationToken cancellationToken)
    {
        var liveVolumeIds = new HashSet<Guid>();

        foreach (var userId in await UserIdsAsync(cancellationToken))
        {
            // A scope per user: CurrentUser is scoped, and ActAs sticks for the whole scope.
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);
            var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

            liveVolumeIds.UnionWith(await dbContext.Volumes
                .Select(volume => volume.Id)
                .ToListAsync(cancellationToken));
        }

        return liveVolumeIds;
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
}
