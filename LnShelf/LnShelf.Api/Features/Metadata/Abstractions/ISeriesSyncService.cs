namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Refreshes every tracked series against the release source, isolating failures
/// per series and never writing user state.
/// </summary>
public interface ISeriesSyncService
{
    Task<SyncRun> RunAsync(CancellationToken cancellationToken);
}

/// <summary>Reads about sync runs: the sidebar's summary and the review list.</summary>
public interface ISyncQueryService
{
    /// <returns>Null when nothing has ever been synced.</returns>
    Task<SyncSummaryResponse?> GetLatestAsync(CancellationToken cancellationToken);
}
