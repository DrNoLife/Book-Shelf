namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Reads for the library grid. Writes live on a separate command service
/// (CLAUDE.md — query/command split at the service boundary).
/// </summary>
public interface ILibraryQueryService
{
    Task<IReadOnlyList<LibraryCardResponse>> GetLibraryAsync(CancellationToken cancellationToken);
}
