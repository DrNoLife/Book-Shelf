using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class MangaUpdatesReleaseSource(
    MangaUpdatesClient client,
    HybridCache cache,
    IOptions<MangaUpdatesOptions> options,
    ILogger<MangaUpdatesReleaseSource> logger) : IReleaseSource
{
    public string SourceName => "MangaUpdates";

    private readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<IReadOnlyList<ReleaseSeriesCandidate>> SearchAsync(
        string title,
        Format format,
        CancellationToken cancellationToken)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length == 0)
        {
            return [];
        }

        var cacheKey = $"mangaupdates:search:{format}:{trimmedTitle.ToLowerInvariant()}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Source: this, Title: trimmedTitle, Format: format),
            static (state, token) => state.Source.FetchSearchAsync(state.Title, state.Format, token),
            _cacheOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<ReleaseSeriesInfo?> GetSeriesAsync(long externalId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(externalId);

        var record = await client.GetSeriesAsync(externalId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var info = MangaUpdatesSeriesMapper.ToSeriesInfo(record);

        logger.LogInformation(
            "Fetched MangaUpdates {MangaUpdatesId} {Title}: {VolumeCount} volumes from status {RawStatus}",
            info.ExternalId,
            info.Title,
            info.VolumeCount,
            info.RawStatus);

        return info;
    }

    private async ValueTask<ReleaseSeriesCandidate[]> FetchSearchAsync(
        string title,
        Format format,
        CancellationToken cancellationToken)
    {
        var response = await client.SearchAsync(
            new MangaUpdatesSearchRequest
            {
                Search = title,
                Type = [.. ToSourceTypes(format)],
                Perpage = options.Value.SearchResultLimit,
            },
            cancellationToken);

        var records = response?.Results ?? [];
        var candidates = new List<ReleaseSeriesCandidate>(records.Count);

        foreach (var result in records)
        {
            if (result.Record is not null)
            {
                candidates.Add(MangaUpdatesSeriesMapper.ToCandidate(result.Record));
            }
        }

        logger.LogInformation(
            "MangaUpdates search {Title} ({Format}) returned {CandidateCount} of {TotalHits} hits",
            title,
            format,
            candidates.Count,
            response?.TotalHits ?? 0);

        return [.. candidates];
    }

    /// <summary>
    /// The shelf's format expressed in MangaUpdates' own vocabulary.
    /// </summary>
    // Filtering matters more here than it did for AniList: an unfiltered search for
    // "Mushoku Tensei" returns 5 872 hits led by doujinshi. Restricted to Novel it
    // returns 172, with the real series on the first page.
    private static string[] ToSourceTypes(Format format) => format switch
    {
        Format.LightNovel => ["Novel"],
        Format.Manga => ["Manga"],

        // A web novel is not something MangaUpdates files separately; "Novel" is as
        // close as its vocabulary gets.
        Format.WebNovel => ["Novel"],
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
