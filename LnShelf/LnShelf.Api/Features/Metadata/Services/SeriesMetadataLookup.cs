namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// One series' record from the series-level metadata source, asked for at most once per
/// refresh however many heals want it.
/// </summary>
// Exists so the description heal and the cover heal share one request when a series lacks
// both: two heals that each asked would spend two of AniList's 30 a minute on the same record.
// The task itself is kept, not just its result, so a request that failed fails both heals
// without being sent a second time; each heal still isolates the failure on its own.
//
// Built per refresh by SeriesRefresher and never registered: a lookup that outlived one
// refresh would serve a stale record to the next.
public sealed class SeriesMetadataLookup(ISeriesMetadataSource metadataSource, int aniListId)
{
    private Task<SeriesMetadata?>? _request;

    public int AniListId => aniListId;

    /// <returns>Null when the source has no series with this id.</returns>
    public Task<SeriesMetadata?> GetAsync(CancellationToken cancellationToken) =>
        _request ??= RequestAsync(cancellationToken);

    // Awaited inside an async method so a source that throws before returning its task still
    // leaves a faulted task behind to remember, rather than nothing and a second request.
    private async Task<SeriesMetadata?> RequestAsync(CancellationToken cancellationToken) =>
        await metadataSource.GetSeriesAsync(aniListId, cancellationToken);
}
