namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class VolumeReadDateCommandService(
    LnShelfDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<VolumeReadDateCommandService> logger) : IVolumeReadDateCommandService
{
    public async Task<SeriesDetailResponse?> SetAsync(
        Guid volumeId,
        SetVolumeReadDateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var today = timeProvider.Today();
        EnsureValid(request.ReadAt, today);

        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(
                candidate => candidate.Volumes.Any(volume => volume.Id == volumeId), cancellationToken);

        if (series?.Volumes.FirstOrDefault(volume => volume.Id == volumeId) is not { } target)
        {
            return null;
        }

        EnsureRead(target);
        target.UserState!.ReadAt = request.ReadAt;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Volume {VolumeNumber} of {SeriesId} was read on {ReadAt}",
            target.Number, series.Id, request.ReadAt);

        // The series' own "last read" date is left alone: it is the most recent reading,
        // not the most recent edit, and correcting an old date must not move it.
        return SeriesDetailProjection.ToDetail(series, today);
    }

    // All or nothing: the client only sends volumes it can see are read, so a refusal here
    // means the page was stale, and a silent partial write would hide that from the user.
    public async Task<SeriesDetailResponse?> SetManyAsync(
        Guid seriesId,
        SetVolumeReadDatesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestedIds = request.VolumeIds?.Distinct().ToList() ?? [];
        if (requestedIds.Count == 0)
        {
            throw new InvalidInputException("Choose at least one volume to set a read date on.");
        }

        var today = timeProvider.Today();
        EnsureValid(request.ReadAt, today);

        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        var targets = series.Volumes.Where(volume => requestedIds.Contains(volume.Id)).ToList();
        if (targets.Count != requestedIds.Count)
        {
            throw new InvalidInputException("One of those volumes is not part of this series.");
        }

        // Checked in full before the first write, so a refusal leaves every volume as it was.
        foreach (var target in targets)
        {
            EnsureRead(target);
        }

        foreach (var target in targets)
        {
            target.UserState!.ReadAt = request.ReadAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Read date of {VolumeCount} volumes of {SeriesId} set to {ReadAt}",
            targets.Count, series.Id, request.ReadAt);

        return SeriesDetailProjection.ToDetail(series, today);
    }

    private static void EnsureValid(DateOnly? readAt, DateOnly today)
    {
        if (readAt > today)
        {
            throw new InvalidInputException("A volume cannot have been read in the future.");
        }

        if (readAt < ReadDateLimits.Earliest)
        {
            throw new InvalidInputException($"A read date cannot be before {ReadDateLimits.Earliest:yyyy-MM-dd}.");
        }
    }

    // A date on a volume that is not read would be the stale lie SetReadAsync avoids by
    // clearing it, and the stats page would believe it.
    private static void EnsureRead(Volume volume)
    {
        if (volume.UserState is not { ReadStatus: ReadStatus.Read })
        {
            throw new InvalidInputException($"Volume {volume.Number} is not marked read.");
        }
    }
}
