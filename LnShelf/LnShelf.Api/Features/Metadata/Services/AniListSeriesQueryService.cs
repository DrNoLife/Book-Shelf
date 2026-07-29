using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// Named per CLAUDE.md's <Source><Entity>QueryService rule: reads from AniList,
// nothing else. Writes to the shelf are SeriesCommandService's job.
public sealed class AniListSeriesQueryService(
    AniListGraphQlClient client,
    HybridCache cache,
    IOptions<AniListOptions> options,
    ILogger<AniListSeriesQueryService> logger) : ISeriesMetadataSource
{
    /// <summary>
    /// One letter matches everything and teaches nothing, so it never leaves the
    /// process — search-as-you-type would spend the whole budget on prefixes.
    /// </summary>
    private const int MinimumQueryLength = 2;

    private static readonly string[] LightNovelFormats = ["NOVEL"];
    private static readonly string[] MangaFormats = ["MANGA", "ONE_SHOT"];
    private static readonly string[] AllTrackedFormats = ["NOVEL", "MANGA", "ONE_SHOT"];

    private readonly HybridCacheEntryOptions _searchCacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,

        // L1 only, explicitly: one API instance, so a distributed layer would be
        // pure ceremony (feature-spec §3, §8).
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

        // AniList has no web-novel format, and WebNovel is manual-only
        // (feature-spec §4.2), so this never becomes a request.
        if (format is Format.WebNovel)
        {
            return [];
        }

        // Keyed on query plus format (feature-spec §8), lower-cased because
        // AniList's own search is case-insensitive: "Mushoku" and "mushoku" are
        // one entry, not two.
        var cacheKey = $"anilist:search:{format?.ToString() ?? "any"}:{trimmedQuery.ToLowerInvariant()}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Source: this, Query: trimmedQuery, Format: format),
            static (state, token) => state.Source.FetchSearchAsync(state.Query, state.Format, token),
            _searchCacheOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<SeriesMetadata?> GetSeriesAsync(int externalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(externalId);

        // Not cached. Adding a series happens once per series, and §8 only asks
        // for the detail cache where the import review queue re-renders the same
        // candidates repeatedly — phase 4.
        var data = await client.ExecuteAsync<AniListMediaData>(
            nameof(AniListQueries.SeriesDetail),
            AniListQueries.SeriesDetail,
            new() { ["id"] = externalId },
            cancellationToken);

        if (data?.Media is null)
        {
            logger.LogWarning("AniList has no series with id {AniListId}", externalId);
            return null;
        }

        var metadata = AniListMediaMapper.ToSeriesMetadata(data.Media);

        logger.LogInformation(
            "Fetched AniList {AniListId} {TitleEnglish} ({Format}): {VolumeCount} volumes, {PublicationStatus}",
            metadata.ExternalId,
            metadata.TitleEnglish,
            metadata.Format,
            metadata.Volumes.Count,
            metadata.PublicationStatus);

        return metadata;
    }

    private async ValueTask<SeriesSearchResult[]> FetchSearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        var data = await client.ExecuteAsync<AniListSearchData>(
            nameof(AniListQueries.SeriesSearch),
            AniListQueries.SeriesSearch,
            new()
            {
                ["search"] = query,
                ["formats"] = ToAniListFormats(format),
                ["perPage"] = options.Value.SearchResultLimit,
            },
            cancellationToken);

        var media = data?.Page?.Media ?? [];
        var results = new List<SeriesSearchResult>(media.Count);

        foreach (var entry in media)
        {
            var result = AniListMediaMapper.ToSearchResult(entry);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        logger.LogInformation(
            "AniList search {Query} ({Format}) returned {ResultCount} trackable of {MediaCount} entries",
            query,
            format?.ToString() ?? "any format",
            results.Count,
            media.Count);

        return [.. results];
    }

    private static string[] ToAniListFormats(Format? format) => format switch
    {
        Format.LightNovel => LightNovelFormats,
        Format.Manga => MangaFormats,

        // WebNovel never reaches here; SearchAsync returns early for it.
        _ => AllTrackedFormats,
    };
}
