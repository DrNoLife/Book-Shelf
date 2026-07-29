using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class VolumeCommandService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    CoverNormalizer coverNormalizer,
    TimeProvider timeProvider,
    ILogger<VolumeCommandService> logger) : IVolumeCommandService
{
    public async Task<VolumeResponse?> SetReadAsync(
        Guid volumeId,
        SetVolumeReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var volume = await LoadVolumeAsync(volumeId, cancellationToken);
        if (volume is null)
        {
            return null;
        }

        var state = EnsureState(volume);
        state.ReadStatus = request.Status;

        // A read date is stamped when a volume becomes read and cleared when it stops
        // being read: keeping a stale date on an unread volume would be a lie the
        // stats page would later believe.
        state.ReadAt = request.Status is ReadStatus.Read
            ? state.ReadAt ?? Today()
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} is now {ReadStatus}",
            volume.Number, volume.SeriesId, request.Status);

        return SeriesDetailProjection.ToVolume(volume, Today());
    }

    public async Task<VolumeResponse?> SetOwnedAsync(
        Guid volumeId,
        SetVolumeOwnedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PurchasePrice is < 0)
        {
            throw new InvalidInputException("A purchase price cannot be negative.");
        }

        var volume = await LoadVolumeAsync(volumeId, cancellationToken);
        if (volume is null)
        {
            return null;
        }

        var state = EnsureState(volume);
        state.Owned = request.Owned;

        if (request.Owned)
        {
            // Physical is the default a book shelf assumes; the format only means
            // anything while the volume is owned.
            state.OwnedFormat = request.Format ?? state.OwnedFormat ?? OwnedFormat.Physical;
            state.PurchasedAt ??= Today();

            if (request.PurchasePrice is not null)
            {
                state.PurchasePrice = request.PurchasePrice;
            }
        }
        else
        {
            state.OwnedFormat = null;

            // The price and the purchase date are kept: un-owning a volume you sold
            // should not erase what you paid for it.
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} is now {OwnedState}",
            volume.Number, volume.SeriesId, request.Owned ? "owned" : "not owned");

        return SeriesDetailProjection.ToVolume(volume, Today());
    }

    public async Task<SeriesDetailResponse?> SetBulkAsync(
        Guid seriesId,
        BulkVolumeStateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ThroughNumber);

        if (request.Read is null && request.Owned is null)
        {
            throw new InvalidInputException("A bulk change has to set read status, ownership, or both.");
        }

        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        var affected = series.Volumes.Where(volume => volume.Number <= request.ThroughNumber).ToList();

        foreach (var volume in affected)
        {
            var state = EnsureState(volume);

            if (request.Read is { } readStatus)
            {
                state.ReadStatus = readStatus;
                state.ReadAt = readStatus is ReadStatus.Read ? state.ReadAt ?? Today() : null;
            }

            if (request.Owned is { } owned)
            {
                state.Owned = owned;
                state.OwnedFormat = owned ? state.OwnedFormat ?? OwnedFormat.Physical : null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bulk change on {SeriesId}: volumes 1–{ThroughNumber} ({AffectedCount} of {TrackedCount}) "
            + "read={Read} owned={Owned}",
            seriesId, request.ThroughNumber, affected.Count, series.Volumes.Count, request.Read, request.Owned);

        return SeriesDetailProjection.ToDetail(series, Today());
    }

    public async Task<VolumeResponse?> SetCoverAsync(
        Guid volumeId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length == 0)
        {
            throw new InvalidInputException("That file is empty.");
        }

        var volume = await LoadVolumeAsync(volumeId, cancellationToken);
        if (volume is null)
        {
            return null;
        }

        // Normalized before anything is stored, so an upload that is not a decodable
        // image is refused rather than served back as one later.
        var normalized = await coverNormalizer.NormalizeAsync(content, cancellationToken)
            ?? throw new InvalidInputException("That file is not an image the shelf can read.");

        var objectKey = CoverObjectKey.ForVolumeOverride(volume.Id, CoverNormalizer.Extension);
        await coverStore.SaveAsync(objectKey, normalized, CoverNormalizer.ContentType, cancellationToken);

        // The key lands on user state, not on the volume: it is user data, and sync
        // writes the volume but never the state (CLAUDE.md).
        var state = EnsureState(volume);
        state.CoverOverrideObjectKey = objectKey;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} has a cover override at {ObjectKey}",
            volume.Number, volume.SeriesId, objectKey);

        return SeriesDetailProjection.ToVolume(volume, Today());
    }

    public async Task<VolumeResponse?> RemoveCoverAsync(Guid volumeId, CancellationToken cancellationToken)
    {
        var volume = await LoadVolumeAsync(volumeId, cancellationToken);
        if (volume is null)
        {
            return null;
        }

        // The stored object is left in place: dropping the reference is what "revert to
        // synced art" means, and keeping the bytes makes an accidental removal
        // recoverable rather than final.
        if (volume.UserState is { } state)
        {
            state.CoverOverrideObjectKey = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} is back to its synced cover",
            volume.Number, volume.SeriesId);

        return SeriesDetailProjection.ToVolume(volume, Today());
    }

    /// <summary>
    /// A volume with no user-state row is Unread and NotOwned; touching it is what
    /// creates the row.
    /// </summary>
    private static VolumeUserState EnsureState(Volume volume) =>
        volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };

    private Task<Volume?> LoadVolumeAsync(Guid volumeId, CancellationToken cancellationToken) =>
        dbContext.Volumes
            .Include(volume => volume.UserState)
            .FirstOrDefaultAsync(volume => volume.Id == volumeId, cancellationToken);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
}
