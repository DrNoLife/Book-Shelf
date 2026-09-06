namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Everything about adding a series that is the same whichever catalogue the id came
/// from: find it if the shelf already has it, build it, save it, then adopt its ISBNs
/// and refresh it.
/// </summary>
// A collaborator of SeriesCommandService rather than more methods on it. The two halves
// share nothing — this one owns the metadata sources and the refresh, the service owns
// the user-state writes and the delete — and together they were a 340-line class doing
// two jobs (CLAUDE.md — fix size by extracting collaborators).
//
// The source-specific half lives behind IAddSeriesFlow, one implementation per source.
public sealed class AddSeriesPipeline(
    LnShelfDbContext dbContext,
    IEnumerable<IAddSeriesFlow> addSeriesFlows,
    IIsbnBackfill isbnBackfill,
    ISeriesSyncService seriesSync,
    ILogger<AddSeriesPipeline> logger)
{
    /// <returns>Null when the source has no series with that id.</returns>
    public async Task<AddSeriesResponse?> RunAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var flow = FlowFor(AddSeriesSource.Of(request));
        var sourceId = flow.SourceIdOf(request);

        var existing = await FindTrackedAsync(flow.Identifies(sourceId), cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "{Source} series {SourceId} is already tracked as {SeriesId}; nothing written",
                flow.Source,
                sourceId,
                existing.SeriesId);

            return existing;
        }

        var series = await flow.BuildAsync(request, sourceId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        WarnOnFormatDisagreement(request.Format, series.Format, flow.Source, sourceId);

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Added {TitleEnglish} ({Format}) from {Source} {SourceId} as {SeriesId} with {VolumeCount} volumes",
            series.TitleEnglish,
            series.Format,
            flow.Source,
            sourceId,
            series.Id,
            series.Volumes.Count);

        return ToAddedResponse(series, await CompleteAsync(series.Id, cancellationToken));
    }

    /// <summary>
    /// The registered flow for a source.
    /// </summary>
    // A lookup over what is registered rather than a switch with a fallthrough: a source
    // nothing can add from is a 400 saying exactly that, where the fallthrough used to
    // route it silently to AniList and add the wrong series.
    private IAddSeriesFlow FlowFor(MetadataSourceName source) =>
        addSeriesFlows.FirstOrDefault(flow => flow.Source == source)
        ?? throw new InvalidInputException($"This shelf cannot add a series from {source}.");

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
    /// Everything a newly added series needs beyond its own source: the ISBNs the shelf
    /// already knows for it, then a refresh against the release and enrichment sources.
    /// </summary>
    // A series source supplies titles, a cover and a volume *list* — and nothing else that
    // is true about the volumes themselves. Without this, a freshly added series sits with
    // no in-print flags, no dates, no volume art and no titles until the nightly job runs,
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
