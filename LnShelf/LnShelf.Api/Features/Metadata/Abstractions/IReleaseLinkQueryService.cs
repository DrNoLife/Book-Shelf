namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Searches the release source on behalf of a person linking a series by hand.
/// </summary>
public interface IReleaseLinkQueryService
{
    /// <param name="query">
    /// The words to search for. Blank searches the series' own title, the way the sync's
    /// matcher starts.
    /// </param>
    /// <returns>Null when no series has that id.</returns>
    /// <exception cref="InvalidInputException">A query longer than any title.</exception>
    Task<ReleaseLinkCandidatesResponse?> GetCandidatesAsync(
        Guid seriesId, string? query, CancellationToken cancellationToken);
}
