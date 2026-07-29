namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for series metadata (CLAUDE.md — seam-first design). AniList is
/// the first implementation; a second source is a new implementation, not an
/// edit. Nothing outside <c>Features/Metadata</c> knows what AniList is.
/// </summary>
// Release dates are deliberately not this seam's job: they belong to
// IReleaseSource (MangaUpdates, phase 2), which wins on dates by the conflict
// rule. This seam owns search, series detail and the volume list.
public interface ISeriesMetadataSource
{
    /// <summary>
    /// Searches the source. Never throws for an empty or too-short query — it
    /// returns nothing, because search-as-you-type must not spend the request
    /// budget on a single letter.
    /// </summary>
    /// <param name="format">Null searches every tracked format.</param>
    Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken);

    /// <summary>
    /// Full metadata for one series, including its volume list.
    /// </summary>
    /// <param name="externalId">The source's own id for the series.</param>
    /// <returns>Null when the source has no series with that id.</returns>
    Task<SeriesMetadata?> GetSeriesAsync(int externalId, CancellationToken cancellationToken);
}
