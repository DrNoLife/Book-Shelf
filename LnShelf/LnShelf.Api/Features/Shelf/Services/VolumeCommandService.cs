namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class VolumeCommandService(
    LnShelfDbContext dbContext,
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
            StampLastRead(series.UserState ??= SeriesUserState.Untouched(series.Id), timeProvider.Today());
        }

        AdvanceTrackingStatus(series);

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

        if (request.PurchasePrice is > VolumeUserState.MaximumPurchasePrice)
        {
            throw new InvalidInputException(
                $"A purchase price can be at most {VolumeUserState.MaximumPurchasePrice}.");
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

        // A 400 rather than ArgumentOutOfRangeException: the number is the caller's input,
        // and the central handler reads any other exception as a defect here.
        if (request.ThroughNumber <= 0)
        {
            throw new InvalidInputException("A bulk change has to run through volume 1 or later.");
        }

        if (request.Read is null && request.Owned is null)
        {
            throw new InvalidInputException("A bulk change has to set read status, ownership, or both.");
        }

        // Checked even where it would be ignored: a number outside the enum is a caller bug
        // worth hearing about, not something to store the next time it is honoured.
        if (request.Format is { } format && !Enum.IsDefined(format))
        {
            throw new InvalidInputException("That is not an edition a volume can be owned in.");
        }

        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        var outcome = BulkVolumeStateWriter.ApplyThrough(
            series,
            request.ThroughNumber,
            request.Read,
            request.Owned,
            stampedOn: timeProvider.Today(),
            ownedFormat: request.Owned is true ? request.Format : null);

        if (outcome.AnyBecameRead)
        {
            // The series is already loaded with its user state here, so no second read.
            StampLastRead(series.UserState ??= SeriesUserState.Untouched(series.Id), timeProvider.Today());
        }

        if (request.Read is not null)
        {
            AdvanceTrackingStatus(series);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Bulk change on {SeriesId}: volumes 1–{ThroughNumber} ({AffectedCount} of {TrackedCount}) "
            + "read={Read} owned={Owned} format={OwnedFormat}",
            seriesId, request.ThroughNumber, outcome.AffectedCount, series.Volumes.Count, request.Read, request.Owned,
            request.Format);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    /// <summary>
    /// A volume with no user-state row is Unread and NotOwned; touching it is what
    /// creates the row.
    /// </summary>
    private static VolumeUserState EnsureState(Volume volume) =>
        volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };

    /// <summary>
    /// Moves the series' tracking status forward when what has been read says it should:
    /// started, or finished.
    /// </summary>
    // Runs on the series as loaded, before the save, so the status lands in the same write as
    // the read state that caused it. The rule itself never moves a status backwards.
    private void AdvanceTrackingStatus(Series series)
    {
        var previous = series.UserState?.TrackingStatus ?? TrackingStatus.PlanToStart;
        var next = TrackingStatusRules.NextStatus(series, timeProvider.Today());
        if (next == previous)
        {
            return;
        }

        series.UserState ??= SeriesUserState.Untouched(series.Id);
        series.UserState.TrackingStatus = next;

        logger.LogInformation(
            "Tracking status of {SeriesId} moved from {PreviousStatus} to {TrackingStatus} after a read change",
            series.Id, previous, next);
    }

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
            .Include(series => series.SourceChoices)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(
                series => series.Volumes.Any(volume => volume.Id == volumeId), cancellationToken);
}

