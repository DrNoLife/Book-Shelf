namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Series-level metadata from MangaUpdates: the search the add flow shows, and the
/// full record it persists.
/// </summary>
// Its own seam rather than a second <see cref="ISeriesMetadataSource"/> implementation,
// which is what the seam-first design would otherwise call for. It cannot be one:
// ISeriesMetadataSource.GetSeriesAsync takes an int, because that is AniList's id shape,
// and MangaUpdates numbers series with eleven digits. Widening that seam would ripple
// through the import matcher and the add flow to buy nothing — both are AniList-only by
// nature — so the honest arrangement is two narrow seams and one composite over them
// (<see cref="ISeriesSearchService"/>), which is what every caller actually wants.
//
// Distinct from IReleaseSource, which the same API also backs: that seam promises volume
// counts and link candidates, and deliberately promises nothing about synopses, covers or
// what a person should see on a search page.
public interface IMangaUpdatesSeriesQueryService
{
    /// <summary>
    /// Searches MangaUpdates. Never throws for an empty or too-short query — it
    /// returns nothing, for the same reason the AniList search does.
    /// </summary>
    /// <param name="format">Null searches every tracked format.</param>
    Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken);

    /// <summary>Full metadata for one series.</summary>
    /// <returns>Null when MangaUpdates has no series with that id.</returns>
    Task<MangaUpdatesSeriesMetadata?> GetSeriesAsync(long seriesId, CancellationToken cancellationToken);
}
