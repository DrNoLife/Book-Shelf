namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class SeriesCommandService(
    LnShelfDbContext dbContext,
    ISeriesMetadataSource metadataSource,
    ICoverCache coverCache,
    TimeProvider timeProvider,
    ILogger<SeriesCommandService> logger) : ISeriesCommandService
{
    public async Task<AddSeriesResponse?> AddFromAniListAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.AniListId);

        var existing = await FindTrackedAsync(request.AniListId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "AniList series {AniListId} is already tracked as {SeriesId}; nothing written",
                request.AniListId,
                existing.SeriesId);

            return existing;
        }

        var metadata = await metadataSource.GetSeriesAsync(request.AniListId, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        if (metadata.Format != request.Format)
        {
            // The id already determines the format, so the source wins and the
            // response reports what was stored. Logged because the only way to
            // get here is a client working from stale search results.
            logger.LogWarning(
                "Add requested {RequestedFormat} for AniList {AniListId}, which is a {SourceFormat}; storing the source's format",
                request.Format,
                request.AniListId,
                metadata.Format);
        }

        // Fetched now rather than on first render: the shelf has to work with the
        // AniList CDN down, which means the bytes must already be local.
        var coverObjectKey = await coverCache.EnsureCachedAsync(
            metadata.ExternalId, metadata.CoverImageUrl, cancellationToken);

        var series = SeriesMetadataMapper.ToTrackedSeries(
            metadata, timeProvider.GetUtcNow(), coverObjectKey);

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added {TitleEnglish} ({Format}) from AniList {AniListId} as {SeriesId} with {VolumeCount} volumes",
            series.TitleEnglish,
            series.Format,
            metadata.ExternalId,
            series.Id,
            series.Volumes.Count);

        return new AddSeriesResponse
        {
            SeriesId = series.Id,
            TitleEnglish = series.TitleEnglish,
            Format = series.Format,
            VolumeCount = series.Volumes.Count,
            AlreadyTracked = false,
        };
    }

    /// <summary>
    /// Idempotency as a read, not a conflict. The unique index on
    /// <c>AniListId</c> is the backstop; this lookup is the behaviour, and it
    /// also spares AniList a request for a series already on the shelf.
    /// </summary>
    private Task<AddSeriesResponse?> FindTrackedAsync(int aniListId, CancellationToken cancellationToken) =>
        dbContext.Series
            .AsNoTracking()
            .Where(series => series.AniListId == aniListId)
            .Select(series => new AddSeriesResponse
            {
                SeriesId = series.Id,
                TitleEnglish = series.TitleEnglish,
                Format = series.Format,
                VolumeCount = series.Volumes.Count,
                AlreadyTracked = true,
            })
            .FirstOrDefaultAsync(cancellationToken);
}
