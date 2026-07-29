namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// One book from the export, parsed into the shelf's own terms.
/// </summary>
public sealed record StoryGraphBook
{
    /// <summary>1-based line in the file, so a bad parse can be traced back.</summary>
    public required int LineNumber { get; init; }

    public required string RawTitle { get; init; }

    /// <summary>The series title as written, volume part removed.</summary>
    public required string SeriesTitle { get; init; }

    /// <summary>Normalised <see cref="SeriesTitle"/> — the grouping key.</summary>
    public required string SeriesKey { get; init; }

    public int? VolumeNumber { get; init; }
    public string? Author { get; init; }
    public string? Isbn { get; init; }

    public required ReadStatus ReadStatus { get; init; }

    /// <summary>
    /// What StoryGraph's own "Owned?" column said, and nothing more.
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
