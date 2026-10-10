namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Edits when a read volume was read. Split from <see cref="IVolumeCommandService"/>,
/// which writes the read status itself: this only ever corrects a date that status
/// already stamped, and never changes what is read.
/// </summary>
public interface IVolumeReadDateCommandService
{
    /// <summary>Sets the read date of one volume, or clears it with a null date.</summary>
    /// <returns>The refreshed series, or null when there is no such volume.</returns>
    /// <exception cref="Errors.InvalidInputException">
    /// The volume is not read, or the date is in the future or before 1900.
    /// </exception>
    Task<SeriesDetailResponse?> SetAsync(
        Guid volumeId,
        SetVolumeReadDateRequest request,
        CancellationToken cancellationToken);

    /// <summary>Sets the read date of several volumes of one series.</summary>
    /// <returns>The refreshed series, or null when there is no such series.</returns>
    /// <exception cref="Errors.InvalidInputException">
    /// No volumes are named, one is not in the series or not read, or the date is invalid.
    /// Nothing is written in any of those cases.
    /// </exception>
    Task<SeriesDetailResponse?> SetManyAsync(
        Guid seriesId,
        SetVolumeReadDatesRequest request,
        CancellationToken cancellationToken);
}
