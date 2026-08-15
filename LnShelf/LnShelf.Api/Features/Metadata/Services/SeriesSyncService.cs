namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// Owns what a *run* is — creating it, choosing which series it covers, isolating failures
// and writing the line the sidebar prints. What happens to one series lives in
// SeriesRefresher, so the nightly sweep and an on-demand refresh cannot drift apart
// (feature-spec §4.2 — "same code path").
public sealed class SeriesSyncService(
    LnShelfDbContext dbContext,
    SeriesRefresher refresher,
    IReleaseSource releaseSource,
    IVolumeCatalogueSource catalogueSource,
    IVolumeEnrichmentSource enrichmentSource,
    IOptions<OpenLibraryOptions> enrichmentOptions,
    TimeProvider timeProvider,
    ILogger<SeriesSyncService> logger) : ISeriesSyncService
{
    public async Task<SyncRun> RunAsync(CancellationToken cancellationToken)
    {
        var run = await StartRunAsync(cancellationToken);

        // Sync skips any series without an external id (feature-spec §4.2) — a
        // manual WebNovel entry has nothing to refresh against.
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Where(candidate => candidate.AniListId != null)
            .OrderBy(candidate => candidate.TitleEnglish)
            .ToListAsync(cancellationToken);

        var linkedExternalIds = series
            .Where(candidate => candidate.MangaUpdatesId != null)
            .Select(candidate => candidate.MangaUpdatesId!.Value)
            .ToHashSet();

        // One budget for the whole sweep, spent down series by series. Enrichment re-asks
        // about the volumes it failed on, because the catalogue grows — which makes an
        // uncapped run able to spend an hour against somebody else's donated API.
        var budget = new LookupBudget(enrichmentOptions.Value.MaximumLookupsPerRun);

        foreach (var candidate in series)
        {
            await RefreshOneAsync(candidate, run, linkedExternalIds, budget, cancellationToken);
        }

        return await FinishRunAsync(run, $"{run.SeriesRefreshed} series refreshed", cancellationToken);
    }

    public async Task<SyncRun?> RunForSeriesAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        var run = await StartRunAsync(cancellationToken);

        // Every *other* series' link, so this one cannot be given a record already spoken
        // for. Its own is excluded, or an already-linked series would look like a clash
        // with itself.
        var linkedExternalIds = (await dbContext.Series
                .Where(candidate => candidate.MangaUpdatesId != null && candidate.Id != seriesId)
                .Select(candidate => candidate.MangaUpdatesId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // A smaller allowance than the nightly sweep's, because somebody is waiting for
        // this one: at twelve lookups a minute a generous budget is a minute-long request.
        var budget = new LookupBudget(enrichmentOptions.Value.MaximumLookupsOnDemand);

        // No AniListId check here, unlike the sweep. Being asked about one series by id is
        // not the same as choosing which series to sweep, and a manual WebNovel entry can
        // still be matched by title.
        await RefreshOneAsync(series, run, linkedExternalIds, budget, cancellationToken);

        return await FinishRunAsync(run, $"{series.TitleEnglish} refreshed", cancellationToken);
    }

    /// <summary>
    /// One series, with the failure isolation that makes a run survive it.
    /// </summary>
    // Per-series failure isolation (CLAUDE.md — the sync job's sanctioned exception to
    // "don't swallow"). Community metadata is flaky; one series failing must not cost the
    // other seven their refresh, and on the single-series path it must not fail the request
    // that triggered it either — the series is already on the shelf by then.
    private async Task RefreshOneAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        try
        {
            await refresher.RefreshAsync(series, run, linkedExternalIds, budget, cancellationToken);
            run.SeriesRefreshed++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            run.Failures++;
            logger.LogWarning(
                exception,
                "Sync failed for {TitleEnglish} (AniList {AniListId}); continuing",
                series.TitleEnglish,
                series.AniListId);
        }
    }

    /// <summary>
    /// Saved before any refreshing, so the discrepancies a refresh records have a run to
    /// belong to.
    /// </summary>
    private async Task<SyncRun> StartRunAsync(CancellationToken cancellationToken)
    {
        var run = new SyncRun
        {
            Id = Guid.CreateVersion7(),
            StartedAt = timeProvider.GetUtcNow(),
        };

        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        return run;
    }

    private async Task<SyncRun> FinishRunAsync(SyncRun run, string lead, CancellationToken cancellationToken)
    {
        run.FinishedAt = timeProvider.GetUtcNow();
        run.Summary = WriteSummary(run, lead);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sync finished: {Summary} ({DiscrepancyCount} discrepancies)",
            run.Summary,
            run.Discrepancies.Count);

        return run;
    }

    /// <summary>
    /// The line the sidebar prints. Transparent, not magical (design.md's voice
    /// rules) — including about what this sync cannot do.
    /// </summary>
    /// <param name="lead">
    /// What the run covered, since it is now either the whole shelf or one series.
    /// </param>
    private string WriteSummary(SyncRun run, string lead)
    {
        var parts = new List<string> { lead };

        if (run.SeriesLinked > 0)
        {
            parts.Add($"{run.SeriesLinked} linked to {releaseSource.SourceName}");
        }

        if (run.NewVolumes > 0)
        {
            parts.Add($"{run.NewVolumes} volumes discovered");
        }

        if (run.VolumesMarkedReleased > 0)
        {
            parts.Add($"{run.VolumesMarkedReleased} volumes confirmed in print");
        }

        if (run.VolumesEnriched > 0)
        {
            parts.Add($"{run.VolumesEnriched} volumes enriched from {enrichmentSource.SourceName}");
        }

        if (run.DatesMoved > 0)
        {
            parts.Add($"{run.DatesMoved} dates written");
        }

        if (run.DescriptionsWritten > 0)
        {
            parts.Add($"{run.DescriptionsWritten} volume blurbs written");
        }

        if (run.VolumeCoversCached > 0)
        {
            parts.Add($"{run.VolumeCoversCached} volume covers cached");
        }

        if (run.VolumesResolvedBySearch > 0)
        {
            // Named as the weaker claim it is: the reader's own ISBN was not indexed, so
            // another printing of the same volume supplied the data.
            parts.Add($"{run.VolumesResolvedBySearch} matched to another printing");
        }

        if (run.EnrichmentGaps > 0)
        {
            // Reported, not hidden: enriching 12 of 20 volumes is the honest number, and
            // a footer that printed only the 12 would read as though it had done all 20.
            parts.Add($"{run.EnrichmentGaps} volumes not in {enrichmentSource.SourceName}");
        }

        if (run.Discrepancies.Count > 0)
        {
            parts.Add($"{run.Discrepancies.Count} to review");
        }

        if (run.Failures + run.EnrichmentFailures > 0)
        {
            parts.Add($"{run.Failures + run.EnrichmentFailures} failed");
        }

        // Said out loud rather than left as a mystery: the release source's word is a
        // count, and everything per-volume comes from two catalogues that each cover part
        // of the shelf. The footer should not imply the shelf knows more than that.
        parts.Add(
            $"{releaseSource.SourceName} counts only; volume detail from "
            + $"{catalogueSource.SourceName} and {enrichmentSource.SourceName}");

        return string.Join(" · ", parts);
    }
}
