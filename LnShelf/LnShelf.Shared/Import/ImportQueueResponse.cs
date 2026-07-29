namespace LnShelf.Shared.Import;

/// <summary>
/// Everything the review page renders, in one round trip: the batch's counts and
/// every group with its candidates.
/// </summary>
public sealed record ImportQueueResponse
{
    public required ImportBatchResponse Batch { get; init; }

    /// <summary>Groups needing a decision first, then the settled ones.</summary>
    public required IReadOnlyList<ImportGroupResponse> Groups { get; init; }
}
