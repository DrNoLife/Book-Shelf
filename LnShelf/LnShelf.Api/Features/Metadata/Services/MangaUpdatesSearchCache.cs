using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The one cached <c>series/search</c> request, shared by both readers of it.
/// </summary>
// There were two cache keys over the identical POST: the release source cached link
// candidates under `mangaupdates:search:`, the add-flow search cached result rows under
// `mangaupdates:series-search:`. Looking a series up and then adding it therefore asked
// MangaUpdates the same question twice, which is not what being a polite API citizen looks
// like (CLAUDE.md). One key and one request now.
//
// What genuinely differed between the two callers was never the request — it was the clean
// shape each wanted out of it, and that is still two mappings. The wire response is what is
// cached, which keeps it the single thing both map from. It travels between two services of
// this slice and no further; nothing outside Features/Metadata sees it.
public sealed class MangaUpdatesSearchCache(
    MangaUpdatesClient client,
    HybridCache cache,
    IOptions<MangaUpdatesOptions> options,
    ILogger<MangaUpdatesSearchCache> logger)
{
    private readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,

        // L1 only, explicitly, like every other cache here: one API instance.
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    /// <param name="title">Already trimmed by the caller; case is folded for the key.</param>
    /// <param name="format">Null means "any tracked medium" — an unnarrowed search.</param>
    public async Task<MangaUpdatesSearchResponse> SearchAsync(
        string title,
        Format? format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(title);

        var cacheKey = $"mangaupdates:search:{format?.ToString() ?? "any"}:{title.ToLowerInvariant()}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Cache: this, Title: title, Format: format),
            static (state, token) => state.Cache.FetchAsync(state.Title, state.Format, token),
            _cacheOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<MangaUpdatesSearchResponse> FetchAsync(
        string title,
        Format? format,
        CancellationToken cancellationToken)
    {
        var response = await client.SearchAsync(
            new MangaUpdatesSearchRequest
            {
                Search = title,
                Type = [.. MangaUpdatesFormats.ToSourceTypes(format)],
                Perpage = options.Value.SearchResultLimit,
            },
            cancellationToken);

        logger.LogInformation(
            "MangaUpdates search {Title} ({Format}) returned {TotalHits} hits",
            title,
            format?.ToString() ?? "any format",
            response?.TotalHits ?? 0);

        // An empty response rather than null, so "the search found nothing" and "the search
        // was not run" are not the same value to every caller downstream.
        return response ?? new MangaUpdatesSearchResponse();
    }
}
