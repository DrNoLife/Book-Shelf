namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Links a tracked series to its counterpart in the release source, or declines to.
/// </summary>
public interface IReleaseSourceMatcher
{
    /// <param name="series">The shelf's series, with the titles AniList gave it.</param>
    /// <param name="alreadyLinkedExternalIds">
    /// Release-source ids other series already hold. The link is unique, so a
    /// candidate that is taken is not a candidate.
    /// </param>
    Task<ReleaseSourceMatch> MatchAsync(
        Series series,
        IReadOnlySet<long> alreadyLinkedExternalIds,
        CancellationToken cancellationToken);
}
