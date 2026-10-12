namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Writes how far the reader is in a series when they count it in chapters: the unit, and the
/// last chapter read. Split from <see cref="ISeriesCommandService"/>, which is already the
/// shelf's busiest writer, because none of its jobs is about chapters.
/// </summary>
public interface ISeriesProgressCommandService
{
    /// <summary>
    /// Counts the series in volumes or in chapters. Switching never touches a volume's read or
    /// owned state, and never the recorded chapter.
    /// </summary>
    /// <returns>The refreshed series, or null when no series has that id.</returns>
    /// <exception cref="Errors.InvalidInputException">Chapters on a light novel.</exception>
    Task<SeriesDetailResponse?> SetProgressUnitAsync(
        Guid seriesId,
        SetProgressUnitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the last chapter read, or clears it with null. A chapter above zero on a
    /// PlanToStart series moves it to Reading (TrackingStatusRules.AfterChapterRecorded).
    /// </summary>
    /// <returns>The refreshed series, or null when no series has that id.</returns>
    /// <exception cref="Errors.InvalidInputException">
    /// The number is outside ChapterProgress' range, or the series is a light novel.
    /// </exception>
    Task<SeriesDetailResponse?> SetLastChapterReadAsync(
        Guid seriesId,
        SetLastChapterReadRequest request,
        CancellationToken cancellationToken);
}
