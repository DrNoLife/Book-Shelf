namespace LnShelf.Shared.Import;

/// <summary>
/// One uploaded export, counted. Rows are books; groups are the series they
/// collapse into, and the group is what gets matched and resolved.
/// </summary>
public sealed record ImportBatchResponse
{
    public required Guid BatchId { get; init; }
    public required string FileName { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }

    /// <summary>Books parsed out of the file.</summary>
    public required int RowCount { get; init; }

    /// <summary>Distinct series those books collapse into.</summary>
    public required int GroupCount { get; init; }

    public required int AutoMatched { get; init; }
    public required int NeedsReview { get; init; }
    public required int Resolved { get; init; }
    public required int Skipped { get; init; }
    public required int Committed { get; init; }

    /// <summary>Null until the batch has been applied to the shelf.</summary>
    public DateTimeOffset? CommittedAt { get; init; }

    /// <summary>Nothing left for the user to decide.</summary>
    public bool IsReadyToCommit => NeedsReview == 0 && CommittedAt is null;
}
