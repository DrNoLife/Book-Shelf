namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// The link is the user's word on which record is this series — metadata the sync could not
// decide, not a local override of something it did decide — so it is written to the same
// MangaUpdatesId column the matcher writes, and the sync treats it like any other link.
public sealed class ReleaseLinkCommandService(
    LnShelfDbContext dbContext,
    IReleaseSource releaseSource,
    ISeriesSyncService syncService,
    SyncRunGate syncRunGate,
    ICurrentUser currentUser,
    ISeriesDetailQueryService seriesDetailQueryService,
    ILogger<ReleaseLinkCommandService> logger) : IReleaseLinkCommandService
{
    public async Task<ReleaseLinkResponse?> LinkAsync(
        Guid seriesId,
        LinkReleaseSourceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MangaUpdatesId <= 0)
        {
            throw new InvalidInputException($"{request.MangaUpdatesId} is not a {releaseSource.SourceName} id.");
        }

        // Tracked, and owner-filtered: another user's id comes back null and answers 404.
        var series = await dbContext.Series.FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        await EnsureLinkableAsync(series, request.MangaUpdatesId, cancellationToken);

        // Any change of link, including a first one: the next refresh would compare the record
        // with volumes it never flagged as released (or with what the old record said), and
        // every volume above that would read as news (a push, a "since your last visit" line,
        // a New badge). Treated as a first refresh instead.
        if (series.MangaUpdatesId != request.MangaUpdatesId)
        {
            series.LastSyncedAt = null;
        }

        series.MangaUpdatesId = request.MangaUpdatesId;
        await ResolveLinkQuestionsAsync(seriesId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "{TitleEnglish} linked by hand to {SourceName} {MangaUpdatesId}",
            series.TitleEnglish,
            releaseSource.SourceName,
            request.MangaUpdatesId);

        var refreshed = await TryRefreshAsync(series, cancellationToken);

        return await seriesDetailQueryService.GetAsync(seriesId, cancellationToken) is { } detail
            ? new ReleaseLinkResponse { Series = detail, Refreshed = refreshed }
            : null;
    }

    private async Task EnsureLinkableAsync(Series series, long mangaUpdatesId, CancellationToken cancellationToken)
    {
        // A series with no AniList id was either added from the release source (and is linked
        // by construction) or added by hand, with nothing for a link to refresh against.
        if (series.AniListId is null)
        {
            throw new ConflictException(
                $"Only a series added from AniList can be linked to {releaseSource.SourceName} by hand.");
        }

        var holder = await dbContext.Series
            .Where(candidate => candidate.MangaUpdatesId == mangaUpdatesId && candidate.Id != series.Id)
            .Select(candidate => candidate.TitleEnglish)
            .FirstOrDefaultAsync(cancellationToken);

        if (holder is not null)
        {
            throw new ConflictException(
                $"That {releaseSource.SourceName} record is already linked to {holder} on your shelf.");
        }

        ReleaseSeriesInfo? info;

        try
        {
            info = await releaseSource.GetSeriesAsync(mangaUpdatesId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw ReleaseSourceUnavailable.From(releaseSource.SourceName, exception);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException
                                           && !cancellationToken.IsCancellationRequested)
        {
            throw ReleaseSourceUnavailable.FromTimeout(releaseSource.SourceName, exception);
        }

        // Checked rather than trusted, because the id is straight from the request body: a link
        // to a record that does not exist would make every later refresh of this series a no-op.
        if (info is null)
        {
            throw new InvalidInputException($"{releaseSource.SourceName} has no series with id {mangaUpdatesId}.");
        }
    }

    /// <summary>
    /// Marks every open "not linked" question about this series answered, by the user.
    /// </summary>
    // In every run that asked it, not only the latest: the review list and the series page read
    // the latest sweep, but an older run's row is the same question and is answered too. Through
    // SyncRuns, which is owner-filtered; the discrepancy table has no filter of its own.
    private async Task ResolveLinkQuestionsAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var runs = await dbContext.SyncRuns
            .Where(run => run.Discrepancies.Any(discrepancy =>
                discrepancy.SeriesId == seriesId
                && discrepancy.Field == SyncDiscrepancyField.SeriesLink
                && discrepancy.ResolvedWith == null))
            .Include(run => run.Discrepancies.Where(discrepancy =>
                discrepancy.SeriesId == seriesId
                && discrepancy.Field == SyncDiscrepancyField.SeriesLink
                && discrepancy.ResolvedWith == null))
            .ToListAsync(cancellationToken);

        foreach (var discrepancy in runs.SelectMany(run => run.Discrepancies))
        {
            discrepancy.ResolvedWith = MetadataSourceName.MangaUpdates;
            discrepancy.ResolvedByChoice = true;
        }
    }

    /// <summary>
    /// Refreshes the series against its new link straight away, the same refresh adding a series
    /// runs, so its volumes do not wait for the night.
    /// </summary>
    /// <returns>Whether the refresh ran and the series came through it.</returns>
    // Behind the same gate as "Sync now" and the nightly loop: a whole-shelf run going at the
    // same time would race this one on the same volumes. Busy is not a failure — the link is
    // saved and the next run picks it up. The run that is going does not: it loaded this series
    // unlinked before the link was saved, so SeriesSyncService re-reads the link from the
    // database before refreshing it, or that run would ask the question again or overwrite the
    // link. The running sweep then refreshes against it, if it has not passed this series yet.
    //
    // Every failure is caught, which is the sync's own rule one level up: the link is the thing
    // asked for and it is already saved; a refresh that could not run must not turn that into
    // an error.
    private async Task<bool> TryRefreshAsync(Series series, CancellationToken cancellationToken)
    {
        using var lease = syncRunGate.TryEnter(currentUser.Id);

        if (lease is null)
        {
            logger.LogInformation(
                "{TitleEnglish} not refreshed after linking: a sync of this shelf is already running",
                series.TitleEnglish);

            return false;
        }

        try
        {
            var run = await syncService.RunForSeriesAsync(series.Id, cancellationToken);
            return run is { Failures: 0 };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "{TitleEnglish} linked, but the refresh after linking failed; the next sync retries",
                series.TitleEnglish);

            return false;
        }
    }
}
