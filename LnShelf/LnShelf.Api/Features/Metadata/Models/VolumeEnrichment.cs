namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What one enrichment source knows about one volume. Every field is optional,
/// because partial coverage is the normal answer rather than the exception.
/// </summary>
// Measured on this shelf's own ISBNs: 63% of records carry both a date and a cover,
// 20% of the ISBNs have no record at all, and 13% have a record with no cover
// (.research/volume-enrichment/e-isbn-first-pipeline.md). A shape that required all
// three would describe a catalogue that does not exist.
public sealed record VolumeEnrichment
{
    /// <summary>The source's own identity for the record, for logs.</summary>
    public required string SourceRecordKey { get; init; }

    /// <summary>
    /// Null when the record has no date, or when its date is too coarse to be one.
    /// </summary>
    // A publish_date of "2022" is a real fact that is not a release date. Widening it
    // to the first of January would be inventing a day the source never claimed —
    // the same reason IsReleased exists instead of a fabricated date.
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>The free-text date exactly as published, kept for the log when it was too coarse.</summary>
    public string? RawPublishDate { get; init; }

    /// <summary>Null for a digital-only edition, which is how 13% of them arrive.</summary>
    public Uri? CoverImageUrl { get; init; }

    /// <summary>The edition's own title, volume part included where the publisher gives one.</summary>
    public string? Title { get; init; }

    public string? Publisher { get; init; }

    /// <summary>What the record says about its own language, including "nothing".</summary>
    public required EditionLanguage Language { get; init; }
}

/// <summary>
/// What an edition record says about its own language.
/// </summary>
// Three states rather than a bool, because the middle one is the common one: plenty of
// genuinely English records simply carry no language field, so "not stated English" and
// "stated Japanese" are very different facts. Treating them the same either throws away
// real editions or puts Japanese art on an English shelf entry.
public enum EditionLanguage
{
    /// <summary>The record names English.</summary>
    English,

    /// <summary>The record names no language at all — common, and not evidence of anything.</summary>
    Unstated,

    /// <summary>The record names a language, and it is not English.</summary>
    Other,
}
