namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for release data (CLAUDE.md — seam-first design). MangaUpdates
/// is the first implementation.
/// </summary>
// What this seam does NOT promise, against feature-spec §4.2's wording: per-volume
// release dates. MangaUpdates' API has no such thing — its /releases endpoints
// return scanlation-group chapter postings, and its series record carries only a
// free-text volume count ("23 LN Volumes (Complete)"). So the seam exposes what a
// release source actually knows: which series it is, whether publication has
// finished, and how many volumes exist. Dates would have to come from somewhere
// else, and inventing them is not an option.
public interface IReleaseSource
{
    /// <summary>The source's name, for logs and the sync summary.</summary>
    string SourceName { get; }

    /// <summary>
    /// Candidates for a title. Search only returns primary titles, which is why
    /// linking needs <see cref="GetSeriesAsync"/> as well.
    /// </summary>
    Task<IReadOnlyList<ReleaseSeriesCandidate>> SearchAsync(
        string title,
        Format format,
        CancellationToken cancellationToken);

    /// <summary>
    /// Full detail for one candidate, including the alternative titles matching
    /// depends on and the volume count reconciliation depends on.
    /// </summary>
    /// <returns>Null when the source has no series with that id.</returns>
    Task<ReleaseSeriesInfo?> GetSeriesAsync(long externalId, CancellationToken cancellationToken);
}
