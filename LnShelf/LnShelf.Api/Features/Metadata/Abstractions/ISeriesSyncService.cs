namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// Refreshes every tracked series against the release source, isolating failures
/// per series and never writing user state.
/// </summary>
public interface ISeriesSyncService
{
    /// <summary>Every series of the current user, with a run's own lookup allowance.</summary>
    Task<SyncRun> RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every series of the current user, spending from <paramref name="budget"/>: the night
    /// shares one allowance across all users' runs.
    /// </summary>
    Task<SyncRun> RunAsync(LookupBudget budget, CancellationToken cancellationToken);

    /// <summary>
    /// The same refresh, for one series, on demand — feature-spec §4.2's "manual sync
    /// now, per series and global, same code path".
    /// </summary>
    // Run the moment a series is added, so a newly shelved series is not blank until
    // 04:00. It is the same work on the same series; only the choice of which series and
    // the size of the lookup allowance differ.
    /// <returns>Null when no series has that id.</returns>
    Task<SyncRun?> RunForSeriesAsync(Guid seriesId, CancellationToken cancellationToken);
}

/// <summary>Reads about sync runs: the sidebar's summary and the review list.</summary>
public interface ISyncQueryService
{
    /// <returns>Null when nothing has ever been synced.</returns>
    Task<SyncSummaryResponse?> GetLatestAsync(CancellationToken cancellationToken);
}
