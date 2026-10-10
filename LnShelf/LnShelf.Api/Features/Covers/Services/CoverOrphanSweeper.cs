namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Deletes uploaded covers whose volume is gone from every shelf.
/// </summary>
// Uploads are user data (GDPR Art. 5(1)(e), 17), and until series deletion started removing
// them they outlived the volume that named them, out of reach of the user and of account
// deletion. This clears what leaked, and what a best-effort delete since then failed to
// remove. Only override/volume/ is swept: an upload is written after its volume row is
// saved, which is what makes the order below safe; synced art (volume/...) has no such
// guarantee.
public sealed class CoverOrphanSweeper(
    IServiceScopeFactory scopeFactory,
    ICoverStore coverStore,
    ILogger<CoverOrphanSweeper> logger)
{
    private const string UploadPrefix = "override/volume/";

    public async Task<CoverOrphanSweepReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        // The keys first, the volumes second. Every key listed was written after its volume
        // was saved, so the volume is in the later read unless it really is gone. The other
        // order would miss a volume added between the two reads and delete its fresh upload.
        var scanned = 0;
        var uploads = new List<(string Key, Guid VolumeId)>();

        await foreach (var key in coverStore.ListKeysAsync(UploadPrefix, cancellationToken))
        {
            scanned++;

            // A key that does not parse as an upload is not this command's to judge.
            if (CoverObjectKey.VolumeIdOf(key) is { } volumeId)
            {
                uploads.Add((key, volumeId));
            }
        }

        // A cover is orphaned when no shelf has its volume, so every user's live volumes are
        // collected, each through the normal owner-filtered context acting as that user.
        var liveVolumeIds = await LiveVolumeIdsOfEveryUserAsync(cancellationToken);

        var orphans = uploads.Where(upload => !liveVolumeIds.Contains(upload.VolumeId)).ToList();
        var deleted = 0;
        var failed = 0;

        if (!dryRun)
        {
            foreach (var (key, _) in orphans)
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
        }

        logger.LogInformation(
            "Cover orphan sweep (dry run: {DryRun}): {Scanned} scanned, {Orphaned} orphaned, {Deleted} deleted, {Failed} failed",
            dryRun, scanned, orphans.Count, deleted, failed);

        return new CoverOrphanSweepReport(scanned, orphans.Count, deleted, failed);
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
}
