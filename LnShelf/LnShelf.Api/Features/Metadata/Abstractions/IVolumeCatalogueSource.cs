namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for "what does a bookstore's catalogue say about this series' volumes?"
/// (CLAUDE.md — seam-first design). Apple Books is the first implementation.
/// </summary>
// The third metadata seam, and a third one on purpose. The two that already exist cannot
// be asked this question:
//
// - ISeriesMetadataSource is series-level by contract. It is keyed by AniList's own media
//   id and answers with facts about the series, not about volume 7.
// - IReleaseSource is series-level too, and deliberately so: MangaUpdates has no
//   per-volume data at all, which is why it answers with one VolumeCount.
// - IVolumeEnrichmentSource *is* per volume, but it is keyed on ISBN — one edition, one
//   request, and only for the 59% of this shelf's volumes whose ISBN is known at all.
//
// This one is keyed on the series title and its format, needs no ISBN, and answers with
// the whole shelf-worth of volumes in a single request. That is a different question, so
// it is a different seam rather than an overload on an existing one (Open/Closed).
//
// The implementation does *not* filter its answer down to volumes that match. Everything
// the store returned comes back, and the caller runs VolumeEditionMatcher over it. A
// store search for "Slime light novel" returns the manga, the German editions and the
// spin-offs alongside the volumes wanted, and deciding which of those is really this
// series' volume N is a conflict rule — those live in one place (VolumeEnrichmentService),
// not once per implementation of this interface.
public interface IVolumeCatalogueSource
{
    /// <summary>The source's name, for logs, discrepancies, and the cover key prefix.</summary>
    string SourceName { get; }

    /// <returns>
    /// Every volume-ish record the store returned, unfiltered and in the store's own
    /// order. Empty when the store has nothing — a normal answer, not a failure.
    /// </returns>
    Task<IReadOnlyList<CatalogueVolume>> GetVolumesAsync(
        string seriesTitle,
        Format format,
        CancellationToken cancellationToken);
}
