namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for per-volume facts (CLAUDE.md — seam-first design). Open Library
/// is the first implementation.
/// </summary>
// A new seam rather than a second ISeriesMetadataSource or IReleaseSource, because
// both of those are series-level by contract: ISeriesMetadataSource is keyed by
// AniList's own media id and its doc comment says nothing outside the slice may know
// what AniList is, and IReleaseSource returns one VolumeCount because MangaUpdates
// has no per-volume data at all. Neither can express "tell me about volume 7".
//
// Keyed on ISBN, not on (series, volume), because that is the only key that works.
// The same catalogue searched by series and volume number answers with Japanese
// editions and omnibuses; asked for one ISBN it answers with one edition
// (.research/volume-enrichment/e-isbn-first-pipeline.md). A source that cannot be
// asked by ISBN is not an implementation of this seam.
public interface IVolumeEnrichmentSource
{
    /// <summary>The source's name, for logs, discrepancies and the sync summary.</summary>
    string SourceName { get; }

    /// <returns>
    /// Null when the source has no record for that ISBN — a normal answer, and the
    /// most common failure by a distance (20% of this shelf's known ISBNs).
    /// </returns>
    Task<VolumeEnrichment?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken);
}
