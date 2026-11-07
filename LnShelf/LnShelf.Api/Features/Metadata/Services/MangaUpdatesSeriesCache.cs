using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The one cached <c>series/{id}</c> request, shared by both readers of it.
/// </summary>
// The sibling of MangaUpdatesSearchCache, for the same reason (CLAUDE.md — be a polite API
// citizen). Uncached, the same record was fetched again seconds after it was first read:
// linking a series during an add reads it to score the candidate, and the refresh that
// completes the add reads it again; adding from MangaUpdates reads it in the add flow and
// again in that refresh. At night every user who tracks the same series asked for it once
// more. Keyed by the MangaUpdates id alone, because the record is the same whoever asks.
//
// The wire record is what is cached, so the release source and the add-flow query service
// keep their own mappings out of one entry. A thrown failure is not cached (HybridCache never
// stores an exception), so a 429 or a 5xx is asked again on the next call. A 404 is cached
// like any answer: for the hour this lasts, a missing id stays missing.
public sealed class MangaUpdatesSeriesCache(
    MangaUpdatesClient client,
    HybridCache cache,
    IOptions<MangaUpdatesOptions> options)
{
    private readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,

        // L1 only, explicitly, like every other cache here: one API instance.
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    /// <returns>Null when MangaUpdates has no series with that id.</returns>
    public async Task<MangaUpdatesSeriesRecord?> GetSeriesAsync(
        long seriesId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seriesId);

        return await cache.GetOrCreateAsync(
            $"mangaupdates:series:{seriesId}",
            (Client: client, SeriesId: seriesId),
            static async (state, token) => await state.Client.GetSeriesAsync(state.SeriesId, token),
            _cacheOptions,
            cancellationToken: cancellationToken);
    }
}
