namespace LnShelf.Api.Features.Covers.Services;

/// <inheritdoc />
// Every write on the request path is scoped by hand to the current user: community_cover is
// not owner-filtered, so nothing else stops a query here from touching another user's row.
// WithdrawAsync is the one deliberate exception and is console-only.
public sealed class CommunityCoverCommandService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<CommunityCoverCommandService> logger) : ICommunityCoverCommandService
{
    public async Task<CoverSharingResponse> SetSharingAsync(bool enabled, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var preferences = await dbContext.UserPreferences.FirstOrDefaultAsync(cancellationToken);

        if (preferences is null)
        {
            preferences = new UserPreferences();
            dbContext.UserPreferences.Add(preferences);
        }

        if (enabled)
        {
            // Kept when already on: the timestamp records when consent was first given.
            preferences.CoverSharingSince ??= timeProvider.GetUtcNow();
            await PublishExistingOverridesAsync(userId, cancellationToken);
        }
        else
        {
            preferences.CoverSharingSince = null;

            // Loaded and removed rather than ExecuteDelete, so the withdrawal lands in the same
            // save as the preference that revokes the consent.
            var published = await dbContext.CommunityCovers
                .Where(cover => cover.UploaderUserId == userId)
                .ToListAsync(cancellationToken);
            dbContext.CommunityCovers.RemoveRange(published);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var sharedCount = await dbContext.CommunityCovers
            .CountAsync(cover => cover.UploaderUserId == userId, cancellationToken);

        logger.LogInformation(
            "Cover sharing for {UserId} set to {Enabled}: {SharedCount} covers shared",
            userId, enabled, sharedCount);

        return new CoverSharingResponse
        {
            Enabled = preferences.CoverSharingSince is not null,
            Since = preferences.CoverSharingSince,
            SharedCount = sharedCount,
        };
    }

    public async Task StageForVolumeAsync(
        Series series,
        Volume volume,
        string? objectKey,
        string? contentHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(volume);

        if (series.AniListId is null && series.MangaUpdatesId is null)
        {
            return;
        }

        var userId = currentUser.Id;
        var sharing = await dbContext.UserPreferences
            .AnyAsync(preferences => preferences.CoverSharingSince != null, cancellationToken);

        if (!sharing)
        {
            return;
        }

        var existing = await dbContext.CommunityCovers.FirstOrDefaultAsync(
            cover => cover.UploaderUserId == userId && cover.SourceVolumeId == volume.Id, cancellationToken);

        if (objectKey is null || contentHash is null)
        {
            if (existing is not null)
            {
                dbContext.CommunityCovers.Remove(existing);
            }

            return;
        }

        Upsert(existing, userId, series, volume, objectKey, contentHash);
    }

    public async Task HideAsync(string contentHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        // Under the owner filter, so this finds the user's own hide and nobody else's.
        if (await dbContext.CommunityCoverHides.AnyAsync(hide => hide.ContentHash == contentHash, cancellationToken))
        {
            return;
        }

        dbContext.CommunityCoverHides.Add(new CommunityCoverHide
        {
            Id = Guid.CreateVersion7(),
            ContentHash = contentHash,
            HiddenAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Hid community cover {ContentHash} for {UserId}", contentHash, currentUser.Id);
    }

    public async Task UnhideAsync(string contentHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var hide = await dbContext.CommunityCoverHides
            .FirstOrDefaultAsync(candidate => candidate.ContentHash == contentHash, cancellationToken);

        if (hide is null)
        {
            return;
        }

        dbContext.CommunityCoverHides.Remove(hide);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Unhid community cover {ContentHash} for {UserId}", contentHash, currentUser.Id);
    }

    public async Task<int> WithdrawAsync(string contentHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var published = await dbContext.CommunityCovers
            .Where(cover => cover.ContentHash == contentHash)
            .ToListAsync(cancellationToken);
        dbContext.CommunityCovers.RemoveRange(published);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Withdrew {Count} community covers with hash {ContentHash}", published.Count, contentHash);

        return published.Count;
    }

    /// <summary>
    /// Publishes every cover the user already uploaded for a volume another user could match.
    /// </summary>
    private async Task PublishExistingOverridesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var volumes = await dbContext.Volumes
            .Include(volume => volume.Series)
            .Include(volume => volume.UserState)
            .Where(volume => volume.UserState != null && volume.UserState.CoverOverrideObjectKey != null)
            .Where(volume => volume.Series!.AniListId != null || volume.Series.MangaUpdatesId != null)
            .ToListAsync(cancellationToken);

        var existing = (await dbContext.CommunityCovers
            .Where(cover => cover.UploaderUserId == userId)
            .ToListAsync(cancellationToken))
            .ToDictionary(cover => cover.SourceVolumeId);

        foreach (var volume in volumes)
        {
            var objectKey = volume.UserState!.CoverOverrideObjectKey!;

            // An override whose object is missing has nothing to publish; the orphan sweep and
            // the volume's own page deal with the dangling key.
            var content = await coverStore.ReadAsync(objectKey, cancellationToken);
            if (content is null)
            {
                continue;
            }

            existing.TryGetValue(volume.Id, out var row);
            Upsert(row, userId, volume.Series!, volume, objectKey, CoverContentHash.Compute(content.Content));
        }
    }

    private void Upsert(
        CommunityCover? existing,
        Guid userId,
        Series series,
        Volume volume,
        string objectKey,
        string contentHash)
    {
        var cover = existing;

        if (cover is null)
        {
            cover = new CommunityCover
            {
                Id = Guid.CreateVersion7(),
                UploaderUserId = userId,
                SourceVolumeId = volume.Id,
            };
            dbContext.CommunityCovers.Add(cover);
        }

        // Refreshed on every stage: the series' ids can arrive later than the cover did.
        cover.AniListId = series.AniListId;
        cover.MangaUpdatesId = series.MangaUpdatesId;
        cover.Format = series.Format;
        cover.Number = volume.Number;
        cover.ObjectKey = objectKey;

        // The publish time moves only when the image does, so "earliest" means who had it first.
        if (existing is null || existing.ContentHash != contentHash)
        {
            cover.ContentHash = contentHash;
            cover.PublishedAt = timeProvider.GetUtcNow();
        }
    }
}
