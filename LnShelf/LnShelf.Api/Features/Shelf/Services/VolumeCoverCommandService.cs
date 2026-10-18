namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class VolumeCoverCommandService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    CoverNormalizer coverNormalizer,
    ICommunityCoverCommandService communityCoverCommand,
    ICommunityCoverQueryService communityCoverQuery,
    TimeProvider timeProvider,
    UserLimits userLimits,
    ILogger<VolumeCoverCommandService> logger) : IVolumeCoverCommandService
{
    public async Task<SeriesDetailResponse?> SetCoverAsync(
        Guid volumeId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length == 0)
        {
            throw new InvalidInputException("That file is empty.");
        }

        var series = await LoadSeriesForVolumeAsync(volumeId, cancellationToken);
        if (series is null || Find(series, volumeId) is not { } volume)
        {
            return null;
        }

        // Replacing a cover takes no extra room; a first one for this volume does.
        if (volume.UserState?.CoverOverrideObjectKey is null)
        {
            await userLimits.EnsureRoomForCoverOverrideAsync(cancellationToken);
        }

        // Normalized before anything is stored, so an upload that is not a decodable
        // image is refused rather than served back as one later.
        var normalized = await coverNormalizer.NormalizeAsync(content, cancellationToken)
            ?? throw new InvalidInputException("That file is not an image the shelf can read.");

        var objectKey = CoverObjectKey.ForVolumeOverride(
            volume.Id, Guid.CreateVersion7(), CoverNormalizer.Extension);
        await coverStore.SaveAsync(objectKey, normalized, CoverNormalizer.ContentType, cancellationToken);

        // The key lands on user state, not on the volume: it is user data, and sync
        // writes the volume but never the state (CLAUDE.md).
        var state = EnsureState(volume);
        var replacedObjectKey = state.CoverOverrideObjectKey;
        state.CoverOverrideObjectKey = objectKey;

        // Staged before the save so the shared row changes in the same write as the override.
        await communityCoverCommand.StageForVolumeAsync(
            series, volume, objectKey, CoverContentHash.Compute(normalized), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Only once the new key is saved: until then the old object is still what the
        // shelf points at.
        await DeleteReplacedOverrideAsync(replacedObjectKey, cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} has a cover override at {ObjectKey}",
            volume.Number, volume.SeriesId, objectKey);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    public async Task<SeriesDetailResponse?> PickCommunityAsync(
        Guid volumeId,
        string contentHash,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var series = await LoadSeriesForVolumeAsync(volumeId, cancellationToken);
        if (series is null || Find(series, volumeId) is not { } volume)
        {
            return null;
        }

        // Only a cover shared for this very volume can be picked: the hash alone would let a
        // user pull in any shared image by guessing at it.
        var candidate = await communityCoverQuery.FindMatchingAsync(series, volume, contentHash, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        // Replacing a cover takes no extra room; a first one for this volume does.
        if (volume.UserState?.CoverOverrideObjectKey is null)
        {
            await userLimits.EnsureRoomForCoverOverrideAsync(cancellationToken);
        }

        // The bytes are copied as they are, not run through the normalizer again: re-encoding
        // would lose quality and change the hash, and the hash is how the shared row recognises
        // this as the same cover.
        var objectKey = CoverObjectKey.ForVolumeOverride(
            volume.Id, Guid.CreateVersion7(), CoverNormalizer.Extension);
        await coverStore.SaveAsync(objectKey, candidate.Content, candidate.ContentType, cancellationToken);

        var state = EnsureState(volume);
        var replacedObjectKey = state.CoverOverrideObjectKey;
        state.CoverOverrideObjectKey = objectKey;

        await communityCoverCommand.StageForVolumeAsync(
            series, volume, objectKey, CoverContentHash.Compute(candidate.Content), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        await DeleteReplacedOverrideAsync(replacedObjectKey, cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} took the community cover {ContentHash}",
            volume.Number, volume.SeriesId, contentHash);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    public async Task<SeriesDetailResponse?> RemoveCoverAsync(
        Guid volumeId,
        CancellationToken cancellationToken)
    {
        var series = await LoadSeriesForVolumeAsync(volumeId, cancellationToken);
        if (series is null || Find(series, volumeId) is not { } volume)
        {
            return null;
        }

        // The stored object goes too. It used to be kept so an accidental removal was
        // recoverable, but every upload now has its own key, so a kept object is one
        // nothing can name again — not a backup, just a leak.
        if (volume.UserState is { CoverOverrideObjectKey: { } removedObjectKey } state)
        {
            state.CoverOverrideObjectKey = null;
            await communityCoverCommand.StageForVolumeAsync(series, volume, null, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await DeleteReplacedOverrideAsync(removedObjectKey, cancellationToken);
        }

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} is back to its synced cover",
            volume.Number, volume.SeriesId);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    /// <summary>
    /// Removes an override object the shelf no longer points at.
    /// </summary>
    // Never fatal. The user's change is already saved by the time this runs, and failing
    // the request over an orphaned 100 KB object would report a write that succeeded as
    // one that did not. The orphan is harmless: nothing references it.
    private async Task DeleteReplacedOverrideAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (objectKey is null)
        {
            return;
        }

        try
        {
            await coverStore.DeleteAsync(objectKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not delete the replaced cover override {ObjectKey}; it is orphaned but unreferenced",
                objectKey);
        }
    }

    /// <summary>
    /// A volume with no user-state row is Unread and NotOwned; touching it is what
    /// creates the row.
    /// </summary>
    private static VolumeUserState EnsureState(Volume volume) =>
        volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };

    /// <summary>The volume being written, within the series that was loaded for it.</summary>
    private static Volume? Find(Series series, Guid volumeId) =>
        series.Volumes.FirstOrDefault(volume => volume.Id == volumeId);

    /// <summary>
    /// The whole series a volume belongs to, in the shape the detail response needs.
    /// </summary>
    // The same load VolumeCommandService does, because every cover write answers with the
    // same response as every other volume write.
    private Task<Series?> LoadSeriesForVolumeAsync(Guid volumeId, CancellationToken cancellationToken) =>
        dbContext.Series
            .Include(series => series.UserState)
            .Include(series => series.SourceChoices)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(
                series => series.Volumes.Any(volume => volume.Id == volumeId), cancellationToken);
}
