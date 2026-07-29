using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Import;

/// <summary>
/// The books of one series, as the review queue shows them.
/// </summary>
public sealed record ImportGroupResponse
{
    public required Guid GroupId { get; init; }

    /// <summary>The title as StoryGraph wrote it, with the volume part removed.</summary>
    public required string SeriesTitle { get; init; }

    public required ImportGroupStatus Status { get; init; }
    public required ImportReviewReason Reason { get; init; }

    /// <summary>Books in this group.</summary>
    public required int RowCount { get; init; }

    /// <summary>
    /// The volume numbers parsed out of the titles, compressed to ranges
    /// ("1–9, 12"). Printed so a wrong parse is visible before committing.
    /// </summary>
    public required string VolumeSummary { get; init; }

    public required int ReadCount { get; init; }

    /// <summary>
    /// StoryGraph does not record ownership, so a read volume is taken to be
    /// owned (feature-spec §4.4). Correctable per group, which is what this is.
    /// </summary>
    public required bool TreatReadAsOwned { get; init; }

    public int? MatchedAniListId { get; init; }
    public int? MatchScore { get; init; }

    /// <summary>Best first. Empty when the source knew nothing by that name.</summary>
    public required IReadOnlyList<ImportCandidateResponse> Candidates { get; init; }

    /// <summary>The rows themselves, for the "what exactly did it read" case.</summary>
    public required IReadOnlyList<ImportRowResponse> Rows { get; init; }
}

/// <summary>One book from the export.</summary>
public sealed record ImportRowResponse
{
    public required int LineNumber { get; init; }
    public required string RawTitle { get; init; }
    public int? VolumeNumber { get; init; }
    public required ReadStatus ReadStatus { get; init; }
    public required bool Owned { get; init; }
    public OwnedFormat? OwnedFormat { get; init; }
    public DateOnly? ReadAt { get; init; }
}
