namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Links a series to its release-source record by hand: the answer to a "Not linked to a
/// release source yet" the sync could not settle.
/// </summary>
public interface IReleaseLinkCommandService
{
    /// <returns>The linked series, or null when no series has that id.</returns>
    /// <exception cref="InvalidInputException">An id the release source has no series for.</exception>
    /// <exception cref="ConflictException">
    /// A series with no AniList id, or a record another series on the shelf is linked to.
    /// </exception>
    Task<ReleaseLinkResponse?> LinkAsync(
        Guid seriesId, LinkReleaseSourceRequest request, CancellationToken cancellationToken);
}
