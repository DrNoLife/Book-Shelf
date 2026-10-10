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
    public Task<SyncRun> RunAsync(CancellationToken cancellationToken) =>
        RunAsync(new LookupBudget(enrichmentOptions.Value.MaximumLookupsPerRun), cancellationToken);

    public async Task<SyncRun> RunAsync(LookupBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);

        var run = await StartRunAsync(coversWholeShelf: true, cancellationToken);

        try
        {
            await SweepAsync(run, budget, cancellationToken);
        }
        catch (Exception exception)
        {
            await FinishInterruptedRunAsync(run, exception, cancellationToken);
            throw;
        }

        return await FinishRunAsync(run, $"{run.SeriesRefreshed} series refreshed", cancellationToken);
    }

    private async Task SweepAsync(SyncRun run, LookupBudget budget, CancellationToken cancellationToken)
    {
        // Sync skips any series without an external id (feature-spec §4.2) — a
        // manual WebNovel entry has nothing to refresh against.
        //
        // *Either* external id counts. It used to be the AniList one alone, which was the
        // same test back when every series arrived from AniList; a series added from
        // MangaUpdates while AniList was disabled has no AniList id at all, and under the
        // narrower filter would have got its one refresh on add and then never been swept
        // again.
        // The user state comes along because the refresh raises HasNewVolume on it — the
        // one field sync writes (CLAUDE.md). Without the Include it would be null here and
        // the badge would never appear.
        //
        // Split: Volumes and SourceChoices are two collections, and one joined query would
        // return every volume once per source choice.
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Where(candidate => candidate.AniListId != null || candidate.MangaUpdatesId != null)
            .OrderBy(candidate => candidate.TitleEnglish)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var linkedExternalIds = series
            .Where(candidate => candidate.MangaUpdatesId != null)
            .Select(candidate => candidate.MangaUpdatesId!.Value)
            .ToHashSet();

        // One budget for the whole sweep, spent down series by series. Enrichment re-asks
        // about the volumes it failed on, because the catalogue grows — which makes an
        // uncapped run able to spend an hour against somebody else's donated API. The
        // caller sizes it: a run's own, or the night's, shared across users.
        foreach (var candidate in series)
        {
            await RefreshOneAsync(candidate, run, linkedExternalIds, budget, cancellationToken);
        }
    }

    public async Task<SyncRun?> RunForSeriesAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        var run = await StartRunAsync(coversWholeShelf: false, cancellationToken);

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
        try
        {
            await RefreshOneAsync(series, run, linkedExternalIds, budget, cancellationToken);
        }
        catch (Exception exception)
        {
            await FinishInterruptedRunAsync(run, exception, cancellationToken);
            throw;
        }

        return await FinishRunAsync(run, $"{series.TitleEnglish} refreshed", cancellationToken);
    }

    /// <summary>
    /// One series, with the failure isolation that makes a run survive it, saved on its own.
    /// </summary>
    // Per-series failure isolation (CLAUDE.md — the sync job's sanctioned exception to
    // "don't swallow"). Community metadata is flaky; one series failing must not cost the
    // other seven their refresh, and on the single-series path it must not fail the request
    // that triggered it either — the series is already on the shelf by then.
    //
    // A save per series rather than one at the end of the run: a shutdown at 04:40 or one
    // row the database refused used to lose the whole night's work. Each series is now its
    // own unit — saved when it succeeds, discarded whole when it does not.
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

            // The run's counters ride along, so the sidebar's figures never get ahead of
            // what is actually saved.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        // Only the caller's own cancellation stops the run. An HttpClient timeout also
        // surfaces as a cancelled task, with nobody having asked for it — and letting that
        // through would abort the sweep over one slow request, leaving the run unfinished
        // and every series after this one unrefreshed. That is one series' failure.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // A DbUpdateException from the save lands here too: a row the database refused is
        // that series' failure, not the run's.
        catch (Exception exception)
        {
            // Everything this series did goes, including the discrepancies it recorded on
            // the run and its share of the run's counters. Keeping the discrepancies would
            // leave the review list asking about a refresh that never landed; the next run
            // meets the same disagreement and records it again.
            dbContext.ChangeTracker.DiscardPendingChanges();

            run.Failures++;
            logger.LogWarning(
                exception,
                "Sync failed for {TitleEnglish} (AniList {AniListId}); continuing",
                series.TitleEnglish,
                series.AniListId);
        }
    }

    /// <summary>
    /// Closes a run that stopped before its last series, so it never sits "in progress"
    /// forever. Best effort: the original failure is the one the caller sees.
    /// </summary>
    // CancellationToken.None when the caller cancelled — that token is already spent, and
    // the shutdown that spent it is exactly when the run row most needs its ending written.
    private async Task FinishInterruptedRunAsync(SyncRun run, Exception exception, CancellationToken cancellationToken)
    {
        // Whatever the interrupted series had half-done is not worth keeping (RefreshOneAsync).
        dbContext.ChangeTracker.DiscardPendingChanges();

        var lead = cancellationToken.IsCancellationRequested
            ? $"Interrupted after {run.SeriesRefreshed} series refreshed"
            : $"Stopped by an error after {run.SeriesRefreshed} series refreshed";

        try
        {
            await FinishRunAsync(run, lead, CancellationToken.None);
        }
        catch (Exception finishException)
        {
            logger.LogError(
                finishException,
                "Could not record the end of sync run {SyncRunId}, which stopped early: {Reason}",
                run.Id,
                exception.Message);
        }
    }

    /// <summary>
    /// Saved before any refreshing, so the discrepancies a refresh records have a run to
    /// belong to.
    /// </summary>
    private async Task<SyncRun> StartRunAsync(bool coversWholeShelf, CancellationToken cancellationToken)
    {
        var run = new SyncRun
        {
            Id = Guid.CreateVersion7(),
            StartedAt = timeProvider.GetUtcNow(),
            CoversWholeShelf = coversWholeShelf,
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

        // Only the ones nobody has settled. A disagreement the precedence list or the user
        // resolved is still recorded, but it is not waiting for anything; the client's own
        // count has always read it this way.
        // Counted per (series, field, volume), the way the review list reads them: one run can
        // record two rows for one volume's date, and that is one item to the person reading.
        var toReview = run.Discrepancies
            .GroupBy(discrepancy => (discrepancy.SeriesId, discrepancy.Field, discrepancy.VolumeNumber))
            .Select(rows => RecordedDisagreement.FromRows(rows, run.StartedAt))
            .Count(disagreement => disagreement.Discrepancy.ResolvedWith is null);
        if (toReview > 0)
        {
            parts.Add($"{toReview} to review");
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
