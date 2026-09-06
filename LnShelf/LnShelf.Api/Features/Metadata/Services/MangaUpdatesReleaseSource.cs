namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class MangaUpdatesReleaseSource(
    MangaUpdatesClient client,
    MangaUpdatesSearchCache searchCache,
    ILogger<MangaUpdatesReleaseSource> logger) : IReleaseSource
{
    public string SourceName => "MangaUpdates";

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

        // The request and its cache entry are shared with the add-flow search, which asks
        // the same endpoint the same question. Only the mapping below is this class's own.
        var response = await searchCache.SearchAsync(trimmedTitle, format, cancellationToken);

        var records = response.Results ?? [];
        var candidates = new List<ReleaseSeriesCandidate>(records.Count);

        foreach (var result in records)
        {
            if (result.Record is not null)
            {
                candidates.Add(MangaUpdatesSeriesMapper.ToCandidate(result.Record));
            }
        }

        logger.LogInformation(
            "MangaUpdates search {Title} ({Format}) offered {CandidateCount} link candidates",
            trimmedTitle,
            format,
            candidates.Count);

        return candidates;
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

}
