namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// The books of one series within a batch — the unit of matching and of review.
/// </summary>
// An addition to feature-spec §5, which models only ImportBatch and a flat
// ImportRow carrying MatchedAniListId/Confidence/Resolution. That shape cannot
// express §4.4's own requirement that per-volume books collapse into one series:
// thirteen Slime rows would each carry a copy of one decision, with nothing
// keeping the copies in step. Matching happens once per series here, which is
// also what keeps 76 books down to 11 AniList queries.
public sealed class ImportGroup
{
    public Guid Id { get; set; }
    public Guid ImportBatchId { get; set; }

    /// <summary>The normalised title the rows were grouped on.</summary>
    public string SeriesKey { get; set; } = string.Empty;

    /// <summary>The title as the export wrote it, volume part removed.</summary>
    public string SeriesTitle { get; set; } = string.Empty;

    public ImportGroupStatus Status { get; set; }
    public ImportReviewReason Reason { get; set; }

    /// <summary>Set once matched, whether automatically or by the user.</summary>
    public int? MatchedAniListId { get; set; }

    /// <summary>Title similarity of the match, 0–100.</summary>
    public int? MatchScore { get; set; }

    /// <summary>
    /// Whether read volumes are taken to be owned (feature-spec §4.4). Defaults
    /// to true and is correctable per group during review.
    /// </summary>
    public bool TreatReadAsOwned { get; set; } = true;

    public ICollection<ImportRow> Rows { get; set; } = [];
    public ICollection<ImportCandidate> Candidates { get; set; } = [];
}
