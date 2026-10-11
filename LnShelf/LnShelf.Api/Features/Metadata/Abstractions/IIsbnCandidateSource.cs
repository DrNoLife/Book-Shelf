namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for "which ISBNs might this volume be?" — the search half of ISBN
/// discovery. Open Library is the first implementation.
/// </summary>
// Deliberately separate from IVolumeEnrichmentSource, which reads one known edition.
// These are different jobs with different confidence: enrichment by ISBN is
// unambiguous, and this is a guess that measured 0 for 7 on volumes whose ISBN was
// unknown (.research/volume-enrichment/e-isbn-first-pipeline.md). Folding the guess
// into the seam that reads facts would let the two be mistaken for each other.
public interface IIsbnCandidateSource
{
    string SourceName { get; }

    /// <summary>
    /// Every ISBN the source associates with that volume, in no particular market.
    /// </summary>
    // The caller has to verify each one: a catalogue search cannot tell an English
    // edition from a Japanese one, so this returns candidates rather than an answer.
    Task<IsbnCandidates> FindAsync(string seriesTitle, decimal volumeNumber, CancellationToken cancellationToken);
}
