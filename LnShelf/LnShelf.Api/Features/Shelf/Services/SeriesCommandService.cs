namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class SeriesCommandService(
    LnShelfDbContext dbContext,
    ISeriesMetadataSource metadataSource,
    IMangaUpdatesSeriesQueryService mangaUpdatesSource,
    ICoverCache coverCache,
    ISeriesCoverCache seriesCoverCache,
    IIsbnBackfill isbnBackfill,
    ISeriesSyncService seriesSync,
    TimeProvider timeProvider,
    ILogger<SeriesCommandService> logger) : ISeriesCommandService
{
    public async Task<AddSeriesResponse?> AddFromAniListAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var aniListId = request.AniListId
            ?? throw new ArgumentException("The request carries no AniList id.", nameof(request));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aniListId);

        var existing = await FindTrackedAsync(series => series.AniListId == aniListId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "AniList series {AniListId} is already tracked as {SeriesId}; nothing written",
                aniListId,
                existing.SeriesId);

            return existing;
        }

        var metadata = await metadataSource.GetSeriesAsync(aniListId, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        WarnOnFormatDisagreement(request.Format, metadata.Format, MetadataSourceName.AniList, aniListId);

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

        return ToAddedResponse(series, await CompleteAsync(series.Id, cancellationToken));
    }

    public async Task<AddSeriesResponse?> AddFromMangaUpdatesAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mangaUpdatesId = request.MangaUpdatesId
            ?? throw new ArgumentException("The request carries no MangaUpdates id.", nameof(request));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mangaUpdatesId);

        // Idempotent on the MangaUpdates id, exactly as the AniList path is on its own:
        // the unique index on the column is the backstop and this lookup is the behaviour.
        // It matters more here than there, because a series linked to MangaUpdates by a
        // nightly sync — never added from it — is still a hit, and adding it again would
        // otherwise be a unique-index violation rather than a polite no-op.
        var existing = await FindTrackedAsync(
            series => series.MangaUpdatesId == mangaUpdatesId, cancellationToken);

        if (existing is not null)
        {
            logger.LogInformation(
                "MangaUpdates series {MangaUpdatesId} is already tracked as {SeriesId}; nothing written",
                mangaUpdatesId,
                existing.SeriesId);

            return existing;
        }

        var metadata = await mangaUpdatesSource.GetSeriesAsync(mangaUpdatesId, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        WarnOnFormatDisagreement(
            request.Format, metadata.Format, MetadataSourceName.MangaUpdates, mangaUpdatesId);

        var coverObjectKey = await seriesCoverCache.EnsureCachedAsync(
            MetadataSourceName.MangaUpdates, metadata.SourceId, metadata.CoverImageUrl, cancellationToken);

        var series = SeriesMetadataMapper.ToTrackedSeries(
            metadata, timeProvider.GetUtcNow(), coverObjectKey);

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added {TitleEnglish} ({Format}) from MangaUpdates {MangaUpdatesId} as {SeriesId} "
            + "with {VolumeCount} volumes from status {RawStatus}",
            series.TitleEnglish,
            series.Format,
            metadata.SourceId,
            series.Id,
            series.Volumes.Count,
            metadata.RawStatus);

        return ToAddedResponse(series, await CompleteAsync(series.Id, cancellationToken));
    }

    /// <summary>
    /// The external id already determines the format, so the source wins and the response
    /// reports what was stored.
    /// </summary>
    // Logged because the only way to get here is a client working from stale search
    // results — worth knowing about, not worth refusing over.
    private void WarnOnFormatDisagreement(
        Format requested,
        Format stored,
        MetadataSourceName source,
        long sourceId)
    {
        if (requested == stored)
        {
            return;
        }

        logger.LogWarning(
            "Add requested {RequestedFormat} for {Source} {SourceId}, which is a {SourceFormat}; "
            + "storing the source's format",
            requested,
            source,
            sourceId,
            stored);
    }

    /// <param name="refreshSummary">What the refresh that ran alongside the add managed.</param>
    // The counts are re-read from the entity rather than from the metadata: the refresh can
    // discover volumes the source did not know about, and reporting the number from before
    // it ran would be wrong the moment it does.
    private static AddSeriesResponse ToAddedResponse(Series series, string? refreshSummary) => new()
    {
        SeriesId = series.Id,
        TitleEnglish = series.TitleEnglish,
        Format = series.Format,
        VolumeCount = series.Volumes.Count,
        AlreadyTracked = false,
        RefreshSummary = refreshSummary,
    };

    /// <summary>
    /// Everything a newly added series needs beyond AniList: the ISBNs the shelf already
    /// knows for it, then a refresh against the release and enrichment sources.
    /// </summary>
    // AniList supplies titles, a cover and a volume *list* — and nothing else that is true
    // about the volumes themselves. Without this, a freshly added series sits with no
    // in-print flags, no dates, no volume art and no titles until the nightly job runs,
    // which is what "I added a series and no real data arrived" was.
    //
    // The ISBN adoption comes first and on purpose: enrichment is keyed on ISBN, so a
    // re-added series whose staged ISBNs have not been restored yet would be skipped
    // volume by volume and the refresh would find nothing to do.
    //
    // Never fatal. The series is already on the shelf by the time this runs, and failing
    // the add because MangaUpdates was down would be the wrong trade — the refresh is an
    // improvement on the add, not part of it.
    private async Task<string?> CompleteAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        try
        {
            var adopted = await isbnBackfill.RunForSeriesAsync(seriesId, cancellationToken);

            if (adopted.Written > 0)
            {
                logger.LogInformation(
                    "Adopted {IsbnCount} ISBN(s) from import staging for {SeriesId}",
                    adopted.Written,
                    seriesId);
            }

            var run = await seriesSync.RunForSeriesAsync(seriesId, cancellationToken);

            return run?.Summary;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not refresh {SeriesId} on add; the nightly sync will pick it up",
                seriesId);

            return null;
        }
    }

    public async Task<SeriesDetailResponse?> SetVolumeViewAsync(
        Guid seriesId,
        SetSeriesVolumeViewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Loaded with its volumes because the answer is the refreshed detail response:
        // the page then redraws from one round trip, as the bulk endpoint already does.
        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        // A series with no user-state row has never been touched; choosing a view is a
        // touch, and it is the same row every other per-series preference lives on.
        var state = series.UserState ??= new SeriesUserState { SeriesId = series.Id };
        state.VolumeViewOverride = request.View;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Series {SeriesId} volume view override is now {VolumeViewOverride}",
            seriesId,
            request.View?.ToString() ?? "the global preference");

        return SeriesDetailProjection.ToDetail(
            series, DateOnly.FromDateTime(timeProvider.GetLocalNow().Date));
    }

    public async Task<bool> DeleteAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return false;
        }

        // Counted before the delete, because afterwards there is nothing to count and
        // the log line is the only record that this shelf ever held them.
        var volumeCount = series.Volumes.Count;

        // RelatedSeriesId is a plain column with no foreign key, so nothing in the
        // database would stop it pointing at a row that no longer exists. Cleared here
        // rather than tolerated: the LN ↔ manga link reads as "tracked separately", and a
        // link to a series that is gone is not that.
        var linkedBack = await dbContext.Series
            .Where(candidate => candidate.RelatedSeriesId == seriesId)
            .ToListAsync(cancellationToken);

        foreach (var candidate in linkedBack)
        {
            candidate.RelatedSeriesId = null;
        }

        // Volumes, both user-state tables and the series' own row go with it, by the
        // cascades configured in SeriesConfiguration and VolumeConfiguration.
        dbContext.Series.Remove(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Cover objects are deliberately left in MinIO, matching what removing a volume's
        // cover override already does: dropping the reference is the operation, and the
        // bytes are a cache. Re-adding the series finds its art already local.
        logger.LogInformation(
            "Deleted {TitleEnglish} ({SeriesId}) with {VolumeCount} volumes and all user state on them; "
            + "{LinkedBackCount} relation(s) cleared",
            series.TitleEnglish,
            seriesId,
            volumeCount,
            linkedBack.Count);

        return true;
    }

    /// <summary>
    /// Idempotency as a read, not a conflict. The unique indexes on the two external id
    /// columns are the backstop; this lookup is the behaviour, and it also spares the
    /// source a request for a series already on the shelf.
    /// </summary>
    /// <param name="isTheSameSeries">
    /// Which external id identifies it — one predicate per source, one projection for both.
    /// </param>
    private Task<AddSeriesResponse?> FindTrackedAsync(
        Expression<Func<Series, bool>> isTheSameSeries,
        CancellationToken cancellationToken) =>
        dbContext.Series
            .AsNoTracking()
            .Where(isTheSameSeries)
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
