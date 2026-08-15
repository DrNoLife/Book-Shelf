using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// Named per CLAUDE.md's <Source><Entity>QueryService rule: reads from MangaUpdates and
// nothing else. It wraps the same MangaUpdatesClient the release source uses, so both go
// through one rate limiter and one set of politeness rules.
public sealed class MangaUpdatesSeriesQueryService(
    MangaUpdatesClient client,
    HybridCache cache,
    IOptions<MangaUpdatesOptions> options,
    ILogger<MangaUpdatesSeriesQueryService> logger) : IMangaUpdatesSeriesQueryService
{
    /// <summary>Same floor as the AniList search, for the same reason.</summary>
    private const int MinimumQueryLength = 2;

    private static readonly string[] LightNovelTypes = ["Novel"];
    private static readonly string[] MangaTypes = ["Manga", "Manhwa", "Manhua"];
    private static readonly string[] AllTrackedTypes = ["Novel", "Manga", "Manhwa", "Manhua"];

    private readonly HybridCacheEntryOptions _searchCacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,

        // L1 only, explicitly, like every other cache here: one API instance.
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length < MinimumQueryLength)
        {
            return [];
        }

        // WebNovel is manual-only (feature-spec §4.2) and MangaUpdates does not file web
        // novels apart from novels anyway, so this never becomes a request — the same
        // answer the AniList search gives.
        if (format is Format.WebNovel)
        {
            return [];
        }

        // A different prefix from the release source's `mangaupdates:search:`, which caches
        // link candidates from the same endpoint: same request, different clean shape, so
        // sharing a key would mean one caller reading the other's answer.
        var cacheKey =
            $"mangaupdates:series-search:{format?.ToString() ?? "any"}:{trimmedQuery.ToLowerInvariant()}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Source: this, Query: trimmedQuery, Format: format),
            static (state, token) => state.Source.FetchSearchAsync(state.Query, state.Format, token),
            _searchCacheOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<MangaUpdatesSeriesMetadata?> GetSeriesAsync(
        long seriesId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seriesId);

        // Not cached, matching the AniList detail query: adding a series happens once per
        // series, and a cache here would only serve a person pressing Add twice.
        var record = await client.GetSeriesAsync(seriesId, cancellationToken);
        if (record is null)
        {
            logger.LogWarning("MangaUpdates has no series with id {MangaUpdatesId}", seriesId);
            return null;
        }

        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(record);

        logger.LogInformation(
            "Fetched MangaUpdates {MangaUpdatesId} {TitleEnglish} ({Format}): "
            + "{VolumeCount} volumes from status {RawStatus}, {PublicationStatus}",
            metadata.SourceId,
            metadata.TitleEnglish,
            metadata.Format,
            metadata.VolumeCount,
            metadata.RawStatus,
            metadata.PublicationStatus);

        return metadata;
    }

    private async ValueTask<SeriesSearchResult[]> FetchSearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        var response = await client.SearchAsync(
            new MangaUpdatesSearchRequest
            {
                Search = query,
                Type = [.. ToSourceTypes(format)],
                Perpage = options.Value.SearchResultLimit,
            },
            cancellationToken);

        var records = response?.Results ?? [];
        var results = new List<SeriesSearchResult>(records.Count);

        foreach (var entry in records)
        {
            if (entry.Record is null)
            {
                continue;
            }

            var result = MangaUpdatesSeriesMapper.ToSearchResult(entry.Record);

            // Filtered on the way out as well as asked for on the way in, the same way the
            // AniList path is: the request narrows the medium and this drops whatever came
            // back regardless — a doujinshi, or a manga on a light-novel search.
            if (result is not null && (format is null || result.Format == format))
            {
                results.Add(result);
            }
        }

        logger.LogInformation(
            "MangaUpdates search {Query} ({Format}) returned {ResultCount} trackable of {TotalHits} hits",
            query,
            format?.ToString() ?? "any format",
            results.Count,
            response?.TotalHits ?? 0);

        return [.. results];
    }

    /// <summary>The shelf's format expressed in MangaUpdates' own vocabulary.</summary>
    // Filtering matters more here than it did for AniList: an unfiltered search for
    // "torture princess" returns thousands of hits led by doujinshi.
    private static string[] ToSourceTypes(Format? format) => format switch
    {
        Format.LightNovel => LightNovelTypes,
        Format.Manga => MangaTypes,

        // WebNovel never reaches here; SearchAsync returns early for it.
        _ => AllTrackedTypes,
    };
}
