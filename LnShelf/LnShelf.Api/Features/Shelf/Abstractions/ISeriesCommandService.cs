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
    /// Adds a series from MangaUpdates: the same shelf row, keyed on the MangaUpdates id
    /// and idempotent on it. What the shelf ends up with differs in what the source knows
    /// — romaji titles more often than English ones, a volume count instead of a volume
    /// list, and a real publisher, which AniList never has.
    /// </summary>
    // Exists because AniList disabling its public API left the app unable to add anything
    // at all. It is not a preference: the AniList path stays the first choice, and this is
    // what the search page's Add button reaches when the results came from the fallback.
    /// <returns>Null when MangaUpdates has no series with that id.</returns>
    Task<AddSeriesResponse?> AddFromMangaUpdatesAsync(
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

    /// <summary>
    /// Removes a series from the shelf, with its volumes and every piece of user state
    /// on them (feature-spec §6).
    /// </summary>
    // Genuinely destructive, and the only endpoint that is: read status, ownership,
    // prices and cover overrides all go with it. There is no soft delete and no undo —
    // one user, one shelf, and a "deleted" flag every query would have to remember to
    // filter is a worse bargain than a confirmation prompt.
    /// <returns>False when no series has that id — nothing to delete is not a failure.</returns>
    Task<bool> DeleteAsync(Guid seriesId, CancellationToken cancellationToken);
}
