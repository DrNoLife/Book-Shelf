using System.Globalization;

namespace LnShelf.Shared.Metadata;

/// <summary>
/// The latest sync run — the sidebar's footer and the review list in one response.
/// </summary>
public sealed record SyncSummaryResponse
{
    public required Guid RunId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }

    public required int SeriesRefreshed { get; init; }
    public required int SeriesLinked { get; init; }
    /// <summary>Structurally zero: no source publishes per-volume dates.</summary>
    public required int DatesMoved { get; init; }

    /// <summary>Volumes the release source confirmed are in print.</summary>
    public required int VolumesMarkedReleased { get; init; }

    public required int NewVolumes { get; init; }
    public required int Failures { get; init; }

    /// <summary>The line the sidebar prints, written by the run itself.</summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Everything the run could not resolve on its own. Unresolved rows first —
    /// they are the ones waiting for a person.
    /// </summary>
    public required IReadOnlyList<SyncDiscrepancyResponse> Discrepancies { get; init; }

    /// <summary>Local time of day, for the footer's "Synced 04:12".</summary>
    public string SyncedAtLabel => StartedAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>One recorded disagreement between sources.</summary>
public sealed record SyncDiscrepancyResponse
{
    public required Guid SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required SyncDiscrepancyField Field { get; init; }
    public string? AniListValue { get; init; }
    public string? MangaUpdatesValue { get; init; }

    /// <summary>Null means unresolved — waiting for a person.</summary>
    public MetadataSourceName? ResolvedWith { get; init; }

    public int? VolumeNumber { get; init; }
    public string? Note { get; init; }

    public bool NeedsReview => ResolvedWith is null;
}
