namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// Named per CLAUDE.md's <Source><Entity>QueryService rule: reads from MangaUpdates and
// nothing else. It wraps the same MangaUpdatesClient the release source uses, so both go
// through one rate limiter and one set of politeness rules.
public sealed class MangaUpdatesSeriesQueryService(
    MangaUpdatesClient client,
    MangaUpdatesSearchCache searchCache,
    ILogger<MangaUpdatesSeriesQueryService> logger) : IMangaUpdatesSeriesQueryService
{
    /// <summary>Same floor as the AniList search, for the same reason.</summary>
    private const int MinimumQueryLength = 2;

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

        // The request and its cache entry are shared with the release source's link search,
        // which asks the same endpoint the same question. Only the mapping below is this
        // class's own.
        var response = await searchCache.SearchAsync(trimmedQuery, format, cancellationToken);

        var records = response.Results ?? [];
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
            "MangaUpdates search {Query} ({Format}) offered {ResultCount} trackable results",
            trimmedQuery,
            format?.ToString() ?? "any format",
            results.Count);

        return results;
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

}
