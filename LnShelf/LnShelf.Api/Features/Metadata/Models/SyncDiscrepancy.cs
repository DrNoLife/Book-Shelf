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

    /// <summary>
    /// What each disagreeing source said, one row per source.
    /// </summary>
    // This was a pair of named columns, AniListValue and MangaUpdatesValue, which meant
    // the schema could hold a disagreement between exactly those two sources and no
    // others. Open Library supplying per-volume dates made that a wall rather than an
    // inconvenience: there was nowhere to put a third opinion. A list of (source, value)
    // pairs says the same thing about two sources and can still say it about four.
    public ICollection<SyncDiscrepancyValue> Values { get; set; } = [];

    /// <summary>Null when nothing was resolved and a person has to decide.</summary>
    public MetadataSourceName? ResolvedWith { get; set; }

    /// <summary>
    /// True when <see cref="ResolvedWith"/> is the user's pick rather than the precedence list's.
    /// </summary>
    // A fact about that run: whether precedence or a person picked the source. The read
    // paths still re-derive "settled by choice" from the choice itself, so a reset is
    // reflected at once without rewriting a stored row.
    public bool ResolvedByChoice { get; set; }

    /// <summary>Set when the disagreement is about one specific volume.</summary>
    public decimal? VolumeNumber { get; set; }

    /// <summary>One line explaining what was found.</summary>
    public string? Note { get; set; }

    /// <summary>Records what one source said. Order of calls is the order shown.</summary>
    public SyncDiscrepancy Says(MetadataSourceName source, string? value)
    {
        Values.Add(new SyncDiscrepancyValue
        {
            Id = Guid.CreateVersion7(),
            SyncDiscrepancyId = Id,
            Source = source,
            Value = value,
        });

        return this;
    }
}

/// <summary>One source's side of a disagreement.</summary>
public sealed class SyncDiscrepancyValue
{
    public Guid Id { get; set; }
    public Guid SyncDiscrepancyId { get; set; }

    public MetadataSourceName Source { get; set; }

    /// <summary>Null when the source's answer was "nothing" — itself a position.</summary>
    public string? Value { get; set; }
}
