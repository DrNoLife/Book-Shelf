using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// The response reuses AniListSearchData: the query's Page.media is the same shape the search
// reads, so a second wire type would only repeat it.
public sealed class AniListRecommendationSource(
    AniListGraphQlClient client,
    HybridCache cache,
    ILogger<AniListRecommendationSource> logger) : IRecommendationSource
{
    /// <summary>AniList's per-page maximum.</summary>
    private const int SeriesPerRequest = 50;

    private const int RecommendationsPerSeries = 10;

    // A day: reader votes move on the scale of weeks, and every series page and For you visit
    // asks.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(24),
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    // Get-only: with DisableUnderlyingData the factory is never run and nothing is written, so
    // the batch can learn which ids are missing and fetch them together instead of one request
    // per id. The tests prove this rather than trusting the documentation.
    private static readonly HybridCacheEntryOptions ReadOnlyOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableUnderlyingData
                | HybridCacheEntryFlags.DisableLocalCacheWrite
                | HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<IReadOnlyDictionary<int, SeriesRecommendations>> GetForSeriesAsync(
        IReadOnlyCollection<int> externalIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, SeriesRecommendations>();
        var missing = new List<int>();

        foreach (var id in externalIds.Distinct())
        {
            var cached = await cache.GetOrCreateAsync<SeriesRecommendations?>(
                KeyFor(id),
                static _ => ValueTask.FromResult<SeriesRecommendations?>(null),
                ReadOnlyOptions,
                cancellationToken: cancellationToken);

            if (cached is null)
            {
                missing.Add(id);
            }
            else
            {
                result[id] = cached;
            }
        }

        // A refusal or a 429 is not caught: nothing is cached for the chunk, so the next visit
        // asks again.
        foreach (var chunk in missing.Chunk(SeriesPerRequest))
        {
            var data = await client.ExecuteAsync<AniListSearchData>(
                nameof(AniListQueries.Recommendations),
                AniListQueries.Recommendations,
                new()
                {
                    ["ids"] = chunk,
                    ["perPage"] = chunk.Length,
                    ["recommendationsPerSeries"] = RecommendationsPerSeries,
                },
                cancellationToken);

            // id_in does not promise an order, so the answer is matched by id.
            var found = (data?.Page?.Media ?? []).ToDictionary(media => media.Id);
            var workCount = 0;

            foreach (var id in chunk)
            {
                // An unknown id is cached empty so it is not asked about on every visit.
                var value = found.TryGetValue(id, out var media)
                    ? AniListRecommendationsMapper.ToRecommendations(media)
                    : SeriesRecommendations.None(id);

                await cache.SetAsync(KeyFor(id), value, CacheOptions, cancellationToken: cancellationToken);

                result[id] = value;
                workCount += value.Works.Count;
            }

            logger.LogInformation(
                "AniList recommends {WorkCount} works across {SeriesCount} series in one request",
                workCount,
                chunk.Length);
        }

        return result;
    }

    // The id alone: the entry holds public data alone and is shared by every shelf. Which works
    // a reader already has, or has dismissed, is removed by the caller per request.
    private static string KeyFor(int externalId) => $"anilist:recommendations:{externalId}";
}
