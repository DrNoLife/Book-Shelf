namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class ManualSeriesCommandService(
    LnShelfDbContext dbContext,
    UserLimits userLimits,
    ICoverStore coverStore,
    TimeProvider timeProvider,
    ILogger<ManualSeriesCommandService> logger) : IManualSeriesCommandService
{
    public async Task<AddSeriesResponse> AddAsync(AddManualSeriesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = ManualSeriesInput.Title(request.TitleEnglish);
        var author = ManualSeriesInput.Author(request.Author);
        var volumeCount = ManualSeriesInput.VolumeCount(request.VolumeCount);
        var format = request.Format;

        // Exact title and format among the shelf's other hand-kept series: the double click,
        // or the form sent twice. Anything looser is left to the reader — two web novels can
        // share a name, and a duplicate is visible and cheap to delete.
        var existing = await dbContext.Series
            .AsNoTracking()
            .Where(series => series.AniListId == null
                             && series.MangaUpdatesId == null
                             && series.Format == format
                             && series.TitleEnglish == title)
            .Select(series => new AddSeriesResponse
            {
                SeriesId = series.Id,
                TitleEnglish = series.TitleEnglish,
                Format = series.Format,
                VolumeCount = series.Volumes.Count,
                AlreadyTracked = true,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        await userLimits.EnsureRoomForSeriesAsync(adding: 1, cancellationToken);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = title,
            Format = format,
            Author = author,

            // The reader decided the count, and the provenance says so: "How is this counted?"
            // then answers "you added these" rather than naming a source that was never asked.
            VolumeCountBasis = VolumeCountBasis.Manual,
        };

        // Created now rather than on the first touch, as every add does, so the series lands on
        // Plan to start like one added from a search.
        series.UserState = SeriesUserState.Untouched(series.Id);

        for (var number = 1; number <= volumeCount; number++)
        {
            series.Volumes.Add(LocalVolume(number));
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added {TitleEnglish} ({Format}) by hand as {SeriesId} with {VolumeCount} volumes",
            series.TitleEnglish,
            series.Format,
            series.Id,
            volumeCount);

        return new AddSeriesResponse
        {
            SeriesId = series.Id,
            TitleEnglish = series.TitleEnglish,
            Format = series.Format,
            VolumeCount = volumeCount,
            AlreadyTracked = false,
        };
    }

    public async Task<SeriesDetailResponse?> AddVolumeAsync(
        Guid seriesId,
        AddLocalVolumeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var series = await LoadAsync(seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        EnsureKeptByHand(series);

        if (series.Volumes.Count >= AddManualSeriesRequest.MaximumVolumeCount)
        {
            throw new ConflictException(
                $"A series added by hand holds at most {AddManualSeriesRequest.MaximumVolumeCount} volumes.");
        }

        var number = ManualSeriesInput.VolumeNumber(request.Number, series.Volumes.Select(volume => volume.Number));

        // Refused rather than merged: the unique index would refuse it anyway, as a 500.
        if (series.Volumes.Any(volume => volume.Number == number))
        {
            throw new ConflictException($"{series.TitleEnglish} already has a volume {number.ToVolumeText()}.");
        }

        series.Volumes.Add(LocalVolume(number));
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added volume {VolumeNumber} to {SeriesId} by hand",
            number,
            series.Id);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    public async Task<SeriesDetailResponse?> RemoveVolumeAsync(
        Guid seriesId,
        Guid volumeId,
        CancellationToken cancellationToken)
    {
        var series = await LoadAsync(seriesId, cancellationToken);
        if (series?.Volumes.FirstOrDefault(candidate => candidate.Id == volumeId) is not { } volume)
        {
            return null;
        }

        EnsureKeptByHand(series);

        // A source's volume would come straight back with the next refresh. Only reachable on a
        // series that once had an id, but the rule belongs to the volume, not to the series.
        if (!volume.IsLocalOnly)
        {
            throw new ConflictException(
                $"Volume {volume.Number.ToVolumeText()} came from a metadata source, so it cannot be removed by hand.");
        }

        // Read before the delete, for the reason SeriesCommandService.DeleteAsync gives: once
        // the row is gone nothing names the uploaded cover, and the user could never remove it.
        var coverObjectKeys = new[] { volume.UserState?.CoverOverrideObjectKey, volume.CoverObjectKey }
            .OfType<string>()
            .Where(objectKey => CoverObjectKey.VolumeIdOf(objectKey) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The volume's user state goes with it, by the cascade in VolumeConfiguration.
        series.Volumes.Remove(volume);
        dbContext.Volumes.Remove(volume);
        await dbContext.SaveChangesAsync(cancellationToken);

        await DeleteCoversAsync(coverObjectKeys, cancellationToken);

        logger.LogInformation(
            "Removed volume {VolumeNumber} from {SeriesId} by hand",
            volume.Number,
            series.Id);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    /// <summary>Loaded the way every write that answers with the series loads it.</summary>
    private Task<Series?> LoadAsync(Guid seriesId, CancellationToken cancellationToken) =>
        dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

    /// <summary>
    /// A linked series' volumes are its sources' to decide (CLAUDE.md: never hand-edit
    /// synced metadata), so only a series with neither id is grown or trimmed by hand.
    /// </summary>
    private static void EnsureKeptByHand(Series series)
    {
        if (series.AniListId is not null || series.MangaUpdatesId is not null)
        {
            throw new ConflictException(
                $"{series.TitleEnglish} is linked to a metadata source, which decides its volumes.");
        }
    }

    /// <summary>
    /// Out, with no date: the reader adding a volume by hand is saying it exists, and nothing
    /// else will ever say so.
    /// </summary>
    private static Volume LocalVolume(decimal number) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        IsReleased = true,
        IsLocalOnly = true,
    };

    /// <summary>Never fatal: the delete is saved, and cover-orphan-sweep removes a leftover upload.</summary>
    private async Task DeleteCoversAsync(IReadOnlyList<string> objectKeys, CancellationToken cancellationToken)
    {
        foreach (var objectKey in objectKeys)
        {
            try
            {
                await coverStore.DeleteAsync(objectKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not delete the cover {ObjectKey} of a removed volume; cover-orphan-sweep removes it if it is an upload",
                    objectKey);
            }
        }
    }
}
