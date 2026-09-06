namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class VolumeCommandService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    CoverNormalizer coverNormalizer,
    TimeProvider timeProvider,
    ILogger<VolumeCommandService> logger) : IVolumeCommandService
{
    public async Task<SeriesDetailResponse?> SetReadAsync(
        Guid volumeId,
        SetVolumeReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var series = await LoadSeriesForVolumeAsync(volumeId, cancellationToken);
        if (series is null || Find(series, volumeId) is not { } volume)
        {
            return null;
        }

        var state = EnsureState(volume);
        var becameRead = request.Status is ReadStatus.Read && state.ReadStatus is not ReadStatus.Read;
        state.ReadStatus = request.Status;

        // A read date is stamped when a volume becomes read and cleared when it stops
        // being read: keeping a stale date on an unread volume would be a lie the
        // stats page would later believe.
        state.ReadAt = request.Status is ReadStatus.Read
            ? state.ReadAt ?? timeProvider.Today()
            : null;

        if (becameRead)
        {
            // The series is already loaded with its user state here, so no second read.
            StampLastRead(series.UserState ??= new SeriesUserState { SeriesId = series.Id }, timeProvider.Today());
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} is now {ReadStatus}",
            volume.Number, volume.SeriesId, request.Status);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    public async Task<SeriesDetailResponse?> SetOwnedAsync(
        Guid volumeId,
        SetVolumeOwnedRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PurchasePrice is < 0)
        {
            throw new InvalidInputException("A purchase price cannot be negative.");
        }

        var series = await LoadSeriesForVolumeAsync(volumeId, cancellationToken);
        if (series is null || Find(series, volumeId) is not { } volume)
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
            state.PurchasedAt ??= timeProvider.Today();

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

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
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
        var anyBecameRead = false;

        foreach (var volume in affected)
        {
            var state = EnsureState(volume);

            if (request.Read is { } readStatus)
            {
                anyBecameRead |=
                    readStatus is ReadStatus.Read && state.ReadStatus is not ReadStatus.Read;

                state.ReadStatus = readStatus;
                state.ReadAt = readStatus is ReadStatus.Read ? state.ReadAt ?? timeProvider.Today() : null;
            }

            if (request.Owned is { } owned)
            {
                state.Owned = owned;
                state.OwnedFormat = owned ? state.OwnedFormat ?? OwnedFormat.Physical : null;
            }
        }

        if (anyBecameRead)
        {
            // The series is already loaded with its user state here, so no second read.
            StampLastRead(series.UserState ??= new SeriesUserState { SeriesId = series.Id }, timeProvider.Today());
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bulk change on {SeriesId}: volumes 1–{ThroughNumber} ({AffectedCount} of {TrackedCount}) "
            + "read={Read} owned={Owned}",
            seriesId, request.ThroughNumber, affected.Count, series.Volumes.Count, request.Read, request.Owned);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

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

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    /// <summary>
    /// A volume with no user-state row is Unread and NotOwned; touching it is what
    /// creates the row.
    /// </summary>
    private static VolumeUserState EnsureState(Volume volume) =>
        volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };

    /// <summary>
    /// Moves the series' "last read" date up to today.
    /// </summary>
    // Forward only, because "last read" is the most recent reading and not the most recent
    // edit: a series read out of order, or an old volume ticked off long after the fact,
    // must not drag the date backwards under the library's Last-read sort.
    //
    // Stamped here rather than derived from the volumes, and that is not a stored derived
    // state: a volume's ReadAt is itself only stamped the first time it is marked, so
    // re-reading a series moves this date while leaving every volume's own date alone.
    private static void StampLastRead(SeriesUserState state, DateOnly today)
    {
        if (state.LastReadAt is null || state.LastReadAt < today)
        {
            state.LastReadAt = today;
        }
    }

    /// <summary>The volume being written, within the series that was loaded for it.</summary>
    private static Volume? Find(Series series, Guid volumeId) =>
        series.Volumes.FirstOrDefault(volume => volume.Id == volumeId);

    /// <summary>
    /// The whole series a volume belongs to, in the shape the detail response needs.
    /// </summary>
    // The same load the bulk path does, because every one of these writes answers with the
    // same response. Loading the volume alone was cheaper per call and cost a second read
    // anyway the moment the series' user state had to be stamped — and it left the counts
    // to be recomputed by whoever was holding the previous response.
    private Task<Series?> LoadSeriesForVolumeAsync(Guid volumeId, CancellationToken cancellationToken) =>
        dbContext.Series
            .Include(series => series.UserState)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(
                series => series.Volumes.Any(volume => volume.Id == volumeId), cancellationToken);
}

