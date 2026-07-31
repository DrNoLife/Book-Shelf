namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Writes to the shelf. Reads live on <see cref="ILibraryQueryService"/>
/// (CLAUDE.md — query/command split at the service boundary).
/// </summary>
public interface ISeriesCommandService
{
    /// <summary>
    /// Adds a series from AniList, with its volume list and a default user-state
    /// row. Idempotent: a series already tracked comes back unchanged, never a
    /// second row.
    /// </summary>
    /// <returns>Null when AniList has no series with that id.</returns>
    Task<AddSeriesResponse?> AddFromAniListAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sets this series' volume view, or clears the override so it follows the global
    /// preference again.
    /// </summary>
    /// <returns>Null when no series has that id.</returns>
    Task<SeriesDetailResponse?> SetVolumeViewAsync(
        Guid seriesId,
        SetSeriesVolumeViewRequest request,
        CancellationToken cancellationToken);
}
