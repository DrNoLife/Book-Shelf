namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The user's explicit pick of which source decides one field — of a series, or of one of
/// its volumes — after the sources disagreed. User data: sync reads it and never writes,
/// replaces or deletes it (CLAUDE.md — the sanctioned override, owner-approved 2026-10-10).
/// </summary>
// It stores the chosen source rather than a frozen value. A frozen value goes stale the first
// time a publisher moves an announced date, which is the exact bug Volume.ReleaseDateSource
// was added to fix (audit 2026-10-09): sync keeps following the chosen source's fresh value.
public sealed class SourceChoice
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }

    /// <summary>The owning series. The owner filter reads it.</summary>
    public Series? Series { get; set; }

    /// <summary>VolumeCount, ReleaseDate or Status. Never SeriesLink.</summary>
    public SyncDiscrepancyField Field { get; set; }

    /// <summary>Set exactly when the field is per volume (ReleaseDate).</summary>
    // A number, not a VolumeId: it is the key SyncDiscrepancy already uses, and (SeriesId,
    // Number) is unique on Volume, so the two join without translation.
    public int? VolumeNumber { get; set; }

    public MetadataSourceName Source { get; set; }

    /// <summary>
    /// What the choice displaced, in <see cref="DiscrepancyValueFormat"/>, so a reset can put
    /// it back. Only a Status choice of MangaUpdates fills it.
    /// </summary>
    // The one field automatic sync cannot re-derive after a reset: the nightly sweep never
    // re-reads AniList, so AniList's "Completed" would be gone for good. The release date
    // and the count re-derive on their own, so they store nothing here.
    public string? ReplacedValue { get; set; }

    public DateTimeOffset ChosenAt { get; set; }
}
