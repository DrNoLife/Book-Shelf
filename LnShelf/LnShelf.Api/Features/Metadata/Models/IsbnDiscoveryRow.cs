namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One volume's ISBN discovery result — a proposal, never a decision.
/// </summary>
// Discovery writes nothing to the shelf. Its measured success rate on volumes with no
// ISBN was 0 of 7 clean resolutions without a follow-up edition lookup
// (.research/volume-enrichment/e-isbn-first-pipeline.md), so the output is a file a
// person reads and the writing is a second, separate command.
public sealed record IsbnDiscoveryRow
{
    public required Guid SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required int VolumeNumber { get; init; }

    /// <summary>The proposed ISBN, or null when nothing survived verification.</summary>
    public string? Isbn { get; init; }

    /// <summary>Open Library's own title for the proposed edition — the evidence to read.</summary>
    public string? OpenLibraryTitle { get; init; }

    public string? Publisher { get; init; }

    public required IsbnDiscoveryOutcome Outcome { get; init; }

    /// <summary>One line saying what happened, including when nothing did.</summary>
    public required string Note { get; init; }
}

/// <summary>Why a volume ended up with an ISBN, or with none.</summary>
public enum IsbnDiscoveryOutcome
{
    /// <summary>
    /// The record says English and says it is this medium — the only fully confident answer.
    /// </summary>
    ConfirmedEnglish,

    /// <summary>
    /// The right series and volume, but the record never says which medium it is.
    /// </summary>
    // The one to actually look at. A real run proposed three Kodansha manga volumes for a
    // Yen Press light novel this way, all titled plainly "…Slime 18" — the right series, the
    // right number, and the wrong book. Nothing else in the row would have given it away, so
    // the outcome says it instead of pretending to be sure.
    MediumUnconfirmed,

    /// <summary>No language on the record, but the publisher is an English-market one.</summary>
    LikelyEnglishPublisher,

    /// <summary>The search returned nothing at all — the common case for a recent volume.</summary>
    NoSearchResults,

    /// <summary>Hits, but not one of them listed an ISBN to verify.</summary>
    NoCandidateIsbns,

    /// <summary>Candidates verified, none of them an English edition.</summary>
    NoEnglishEdition,

    /// <summary>The lookup itself failed. A data gap, recorded rather than retried forever.</summary>
    LookupFailed,
}
