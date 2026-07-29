namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Reads for the library grid. Writes live on a separate command service
/// (CLAUDE.md — query/command split at the service boundary).
/// </summary>
public interface ILibraryQueryService
{
    /// <param name="format">Null means every format — the "All" tab.</param>
    /// <param name="filter">Narrows the cards; totals and shelves ignore it.</param>
    Task<LibraryResponse> GetLibraryAsync(
        Format? format,
        LibraryFilter filter,
        CancellationToken cancellationToken);
}
