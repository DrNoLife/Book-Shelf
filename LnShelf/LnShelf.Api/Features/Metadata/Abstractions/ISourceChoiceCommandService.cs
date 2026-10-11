namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Writes the user's choice of source on a recorded disagreement, and takes it back.
/// </summary>
public interface ISourceChoiceCommandService
{
    /// <returns>The refreshed series, or null when no series has that id.</returns>
    /// <exception cref="InvalidInputException">
    /// SeriesLink; a volume number on a series field, or none on a volume field; a volume that
    /// does not exist; a source with no recorded value for this disagreement.
    /// </exception>
    Task<SeriesDetailResponse?> ChooseAsync(
        Guid seriesId, ChooseSourceRequest request, CancellationToken cancellationToken);

    /// <returns>
    /// The refreshed series, or null when no series has that id. Resetting with no choice is a
    /// success.
    /// </returns>
    Task<SeriesDetailResponse?> ResetAsync(
        Guid seriesId, SyncDiscrepancyField field, decimal? volumeNumber, CancellationToken cancellationToken);
}
