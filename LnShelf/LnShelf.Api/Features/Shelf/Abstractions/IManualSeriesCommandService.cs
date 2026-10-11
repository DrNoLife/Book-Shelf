namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Series the reader keeps by hand: added with no external id, grown and trimmed one
/// local volume at a time (feature-spec §4.2 — WebNovel is manual-only).
/// </summary>
// Apart from ISeriesCommandService on purpose. Adding from a catalogue runs the add pipeline,
// which refreshes the new series and adopts its ISBNs; a series nobody catalogues has nothing
// to refresh, and the title-matching refresh would go looking for a release-source link the
// reader never asked for.
public interface IManualSeriesCommandService
{
    /// <returns>
    /// The added series, or the one already on the shelf under the same title and format
    /// (<see cref="AddSeriesResponse.AlreadyTracked"/>), so a double click adds one.
    /// </returns>
    Task<AddSeriesResponse> AddAsync(AddManualSeriesRequest request, CancellationToken cancellationToken);

    /// <returns>The refreshed series, or null when there is no such series.</returns>
    /// <exception cref="ConflictException">
    /// The series is linked to a metadata source, which owns its volumes; or the number is taken.
    /// </exception>
    Task<SeriesDetailResponse?> AddVolumeAsync(
        Guid seriesId,
        AddLocalVolumeRequest request,
        CancellationToken cancellationToken);

    /// <returns>The refreshed series, or null when there is no such series or volume.</returns>
    /// <exception cref="ConflictException">
    /// The series is linked to a metadata source, or the volume came from one.
    /// </exception>
    Task<SeriesDetailResponse?> RemoveVolumeAsync(
        Guid seriesId,
        Guid volumeId,
        CancellationToken cancellationToken);
}
