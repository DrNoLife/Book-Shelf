namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// A recorded disagreement between metadata sources, per feature-spec §5 — the
/// structured landing place for "recorded, never silently picked".
/// </summary>
public sealed class SyncDiscrepancy
{
    public Guid Id { get; set; }
    public Guid SyncRunId { get; set; }
    public Guid SeriesId { get; set; }

    /// <summary>Denormalised so the review list reads without a join.</summary>
    // A copy of data that lives elsewhere, which is usually wrong — but a
    // discrepancy is a record of what was true when it was recorded, and a renamed
    // series should not silently rewrite its own history.
    public string SeriesTitle { get; set; } = string.Empty;

    public SyncDiscrepancyField Field { get; set; }

    public string? AniListValue { get; set; }
    public string? MangaUpdatesValue { get; set; }

    /// <summary>Null when nothing was resolved and a person has to decide.</summary>
    public MetadataSourceName? ResolvedWith { get; set; }

    /// <summary>Set when the disagreement is about one specific volume.</summary>
    public int? VolumeNumber { get; set; }

    /// <summary>One line explaining what was found.</summary>
    public string? Note { get; set; }
}
