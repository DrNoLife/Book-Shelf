namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One sync run, per feature-spec §5. The latest one is what the sidebar prints.
/// </summary>
public sealed class SyncRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while a run is in flight.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    public int SeriesRefreshed { get; set; }

    /// <summary>Series newly linked to the release source by this run.</summary>
    public int SeriesLinked { get; set; }

    /// <summary>
    /// Volumes whose release date was written or moved.
    /// </summary>
    // No longer structurally zero, as this comment used to say. Open Library supplies
    // per-volume dates for the volumes whose ISBN is known, so a date can now arrive and
    // can now change.
    public int DatesMoved { get; set; }

    /// <summary>Volumes an enrichment source had a record for.</summary>
    public int VolumesEnriched { get; set; }

    /// <summary>Volume covers fetched and stored locally by this run.</summary>
    public int VolumeCoversCached { get; set; }

    /// <summary>
    /// Volumes whose own ISBN was not indexed, but whose data was found under another
    /// printing of the same volume.
    /// </summary>
    // Counted separately from VolumesEnriched because it is a weaker claim: the record came
    // from a name search rather than from the identifier the reader gave, and a run that
    // leaned on it should say so.
    public int VolumesResolvedBySearch { get; set; }

    /// <summary>
    /// Volumes looked up whose ISBN the source has no usable record for.
    /// </summary>
    // Reported rather than hidden: 20% of this shelf's known ISBNs are genuine catalogue
    // gaps, and a run that quietly enriched 12 of 20 volumes should say so.
    public int EnrichmentGaps { get; set; }

    /// <summary>Volume lookups that threw. One failure never stops a run.</summary>
    public int EnrichmentFailures { get; set; }

    /// <summary>
    /// Volumes the release source confirmed are published — what stands in for
    /// dates, and what makes the "to buy" pile possible at all.
    /// </summary>
    public int VolumesMarkedReleased { get; set; }

    /// <summary>Volumes the release source knew about and the shelf did not.</summary>
    public int NewVolumes { get; set; }

    /// <summary>Series whose refresh threw. One failure never stops a run.</summary>
    public int Failures { get; set; }

    /// <summary>The line the sidebar prints, written by the run itself.</summary>
    public string Summary { get; set; } = string.Empty;

    public ICollection<SyncDiscrepancy> Discrepancies { get; set; } = [];
}
