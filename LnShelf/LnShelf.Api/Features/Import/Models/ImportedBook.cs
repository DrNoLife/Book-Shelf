namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// One book from an export, parsed into the shelf's own terms.
/// </summary>
// Source-neutral by construction: every field here is something StoryGraph and
// Goodreads both express, and everything one of them spells its own way stops at
// that source's mapper. This is what lets ImportBatchBuilder group, match and
// stage without knowing which product wrote the file.
public sealed record ImportedBook
{
    /// <summary>1-based line in the file, so a bad parse can be traced back.</summary>
    public required int LineNumber { get; init; }

    public required string RawTitle { get; init; }

    /// <summary>The series title as written, volume part removed.</summary>
    public required string SeriesTitle { get; init; }

    /// <summary>Normalised <see cref="SeriesTitle"/> — the grouping key.</summary>
    public required string SeriesKey { get; init; }

    public int? VolumeNumber { get; init; }

    /// <summary>
    /// This volume's own subtitle, when the export's title carried one and it is in
    /// the language the shelf displays. Null otherwise.
    /// </summary>
    public string? Subtitle { get; init; }

    public string? Author { get; init; }

    /// <summary>
    /// The artist, when the export credits one. Null otherwise.
    /// </summary>
    // Carried for one job: an author is usually credited on both a light novel and
    // its manga adaptation, so it cannot separate them — the illustrator can, and
    // AniListSeriesMatcher uses it to break exactly that tie.
    public string? Illustrator { get; init; }

    public string? Isbn { get; init; }

    public required ReadStatus ReadStatus { get; init; }

    /// <summary>
    /// What the export's own ownership column said, and nothing more.
    /// </summary>
    // Kept separate from effective ownership on purpose: "read implies owned"
    // (feature-spec §4.4) is a rule the user can switch off per series in review,
    // and a rule that has already been baked into the stored value cannot be
    // switched off. ImportOwnershipRule combines the two.
    public required bool OwnedInExport { get; init; }

    public OwnedFormat? OwnedFormat { get; init; }

    /// <summary>Null when the export recorded no date.</summary>
    public DateOnly? ReadAt { get; init; }
}
