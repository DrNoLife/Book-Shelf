namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for reader recommendations (CLAUDE.md, seam-first design). AniList's
/// <c>Media.recommendations</c> is the first implementation; another source is a new
/// implementation, not an edit. Nothing outside <c>Features/Metadata</c> knows what AniList is.
/// </summary>
// Series-level and keyed on the source's own id, like ISeriesMetadataSource: what readers of
// a series also liked is a fact about the series, so one answer serves every shelf.
public interface IRecommendationSource
{
    /// <summary>
    /// What readers of each series also liked.
    /// </summary>
    /// <param name="externalIds">The source's own series ids (<c>Series.AniListId</c>).</param>
    /// <returns>
    /// An entry for EVERY id asked, empty for one the source does not know, so a caller never
    /// has to tell "unknown" from "missing".
    /// </returns>
    /// <exception cref="MetadataSourceUnavailableException">The source could not be reached or refused the request.</exception>
    Task<IReadOnlyDictionary<int, SeriesRecommendations>> GetForSeriesAsync(
        IReadOnlyCollection<int> externalIds,
        CancellationToken cancellationToken);
}
