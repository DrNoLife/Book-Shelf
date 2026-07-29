namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// Reads for the import pages. Writes live on
/// <see cref="IImportCommandService"/> (CLAUDE.md — query/command split).
/// </summary>
public interface IImportQueryService
{
    /// <returns>Null when there is no such batch.</returns>
    Task<ImportBatchResponse?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken);

    /// <returns>Null when there is no such batch.</returns>
    Task<ImportQueueResponse?> GetQueueAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>The most recent batches, newest first — the import page's landing state.</summary>
    Task<IReadOnlyList<ImportBatchResponse>> GetRecentBatchesAsync(
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// The review queue's manual fallback: search the metadata source directly,
    /// with covers cached so the results render like the automatic candidates.
    /// </summary>
    Task<IReadOnlyList<ImportCandidateResponse>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken);
}
