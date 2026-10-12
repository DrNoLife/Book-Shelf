namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What one source states about one volume, in the shape the write rules read it.
/// </summary>
// A third record beside VolumeEnrichment and CatalogueVolume, and not a redundant one: both
// of those describe how their *source* answers, while this describes what a source's answer
// offers the shelf. Two sources map into it, the write rules read only it, and a third
// source is a new mapping rather than a new rule (Open/Closed).
public sealed record VolumeFacts
{
    /// <summary>Which source said it, for the discrepancy row a disagreement becomes.</summary>
    public required MetadataSourceName Source { get; init; }

    /// <summary>
    /// The seam's own <c>SourceName</c>, for logs and the cover key prefix.
    /// </summary>
    // Kept beside <see cref="Source"/> rather than derived from it. The two strings happen
    // to be equal today, and leaning on that would mean a seam renaming its SourceName
    // silently moved every cover it had ever cached.
    public required string SourceName { get; init; }

    /// <summary>The source's own identity for the record, for logs.</summary>
    public required string SourceRecordKey { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Whether this source's date may replace one already on the volume, rather than only
    /// fill an empty one.
    /// </summary>
    // The release-date precedence (CLAUDE.md) as data rather than as a branch on the source's
    // name, so a third per-volume source is a mapping and not an edit to the writer: a real
    // per-edition date, by ISBN, outranks a storefront listing's.
    public bool MayReplaceReleaseDate { get; init; }

    /// <summary>
    /// The date exactly as published, kept for the log when it was too coarse to be a date.
    /// </summary>
    public string? RawReleaseDate { get; init; }

    public Uri? CoverImageUrl { get; init; }

    /// <summary>
    /// Whether the source already ruled its placeholder art out, so a cover on a volume that
    /// is not out yet may be cached.
    /// </summary>
    // Data rather than a branch on the source's name, like MayReplaceReleaseDate: a source
    // that cannot tell a placeholder from a cover leaves this false, and the writer falls back
    // to caching no art before the release date.
    public bool CoverScreenedForPlaceholder { get; init; }
}
