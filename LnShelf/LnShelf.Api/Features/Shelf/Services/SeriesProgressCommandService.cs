namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
// A chapter can be recorded on a manga or web novel whichever unit it is counted in. The page
// only offers the input on a Chapters series, but refusing the write on a Volumes one would
// turn a second tab that switched the unit into an error for a number that is kept anyway.
public sealed class SeriesProgressCommandService(
    LnShelfDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<SeriesProgressCommandService> logger) : ISeriesProgressCommandService
{
    public async Task<SeriesDetailResponse?> SetProgressUnitAsync(
        Guid seriesId,
        SetProgressUnitRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var series = await LoadAsync(seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        if (request.ProgressUnit is ProgressUnit.Chapters)
        {
            EnsureChaptersOffered(series);
        }

        (series.UserState ??= SeriesUserState.Untouched(series.Id)).ProgressUnit = request.ProgressUnit;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Series {SeriesId} progress is now counted in {ProgressUnit}", seriesId, request.ProgressUnit);

        return SeriesDetailProjection.ToDetail(series, timeProvider.Today());
    }

    public async Task<SeriesDetailResponse?> SetLastChapterReadAsync(
        Guid seriesId,
        SetLastChapterReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked before the load, so a bad number is refused without a round trip.
        if (request.LastChapterRead is { } chapter && ChapterProgress.ProblemWith(chapter) is { } problem)
        {
            throw new InvalidInputException(problem);
        }

        var series = await LoadAsync(seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        EnsureChaptersOffered(series);

        var state = series.UserState ??= SeriesUserState.Untouched(series.Id);
        var previousChapter = state.LastChapterRead;
        state.LastChapterRead = request.LastChapterRead;
        state.TrackingStatus = TrackingStatusRules.AfterChapterRecorded(state.TrackingStatus, state.LastChapterRead);

        // A further chapter is a reading, so it moves "last read" up the way marking a volume
        // read does — which is what lets a chapter reader's series lead Continue reading.
        // Forward only, and only for a higher chapter: an undo or a correction is not a reading.
        var today = timeProvider.Today();
        if (request.LastChapterRead > (previousChapter ?? 0) && (state.LastReadAt is null || state.LastReadAt < today))
        {
            state.LastReadAt = today;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Series {SeriesId} last chapter read is now {LastChapterRead}; tracking status {TrackingStatus}",
            seriesId,
            request.LastChapterRead?.ToVolumeText() ?? "cleared",
            state.TrackingStatus);

        return SeriesDetailProjection.ToDetail(series, today);
    }

    // The same graph every series write loads, because the answer is the whole detail response.
    private Task<Series?> LoadAsync(Guid seriesId, CancellationToken cancellationToken) =>
        dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

    private static void EnsureChaptersOffered(Series series)
    {
        if (!ChapterProgress.IsOfferedFor(series.Format))
        {
            throw new InvalidInputException(
                "A light novel is counted in volumes; chapters are for manga and web novels.");
        }
    }
}
