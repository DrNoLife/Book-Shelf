namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SeriesSyncService(
    LnShelfDbContext dbContext,
    IReleaseSourceMatcher releaseSourceMatcher,
    IReleaseSource releaseSource,
    VolumeEnrichmentService volumeEnrichment,
    IVolumeEnrichmentSource enrichmentSource,
    IOptions<OpenLibraryOptions> enrichmentOptions,
    TimeProvider timeProvider,
    ILogger<SeriesSyncService> logger) : ISeriesSyncService
{
    /// <summary>
    /// Per-volume lookups left in this run. A cap on the whole run, not per series.
    /// </summary>
    // Enrichment re-asks about the volumes it failed on, because the catalogue grows —
    // which makes an uncapped run able to spend an hour on gaps against somebody else's
    // donated API. The run takes a bite and leaves the rest for tomorrow.
    private int _lookupBudget;

    public async Task<SyncRun> RunAsync(CancellationToken cancellationToken)
    {
        _lookupBudget = enrichmentOptions.Value.MaximumLookupsPerRun;

        var run = new SyncRun
        {
            Id = Guid.CreateVersion7(),
            StartedAt = timeProvider.GetUtcNow(),
        };

        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        foreach (var candidate in series)
        {
            // Per-series failure isolation (CLAUDE.md — the sync job's sanctioned
            // exception to "don't swallow"). Community metadata is flaky; one series
            // failing must not cost the other seven their refresh.
            try
            {
                await RefreshAsync(candidate, run, linkedExternalIds, cancellationToken);
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
                    candidate.TitleEnglish,
                    candidate.AniListId);
            }
        }

        run.FinishedAt = timeProvider.GetUtcNow();
        run.Summary = WriteSummary(run);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sync finished: {Summary} ({DiscrepancyCount} discrepancies)",
            run.Summary,
            run.Discrepancies.Count);

        return run;
    }

    private async Task RefreshAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        CancellationToken cancellationToken)
    {
        var info = series.MangaUpdatesId is { } externalId
            ? await releaseSource.GetSeriesAsync(externalId, cancellationToken)
            : await LinkAsync(series, run, linkedExternalIds, cancellationToken);

        // Enrichment runs whether or not the release source had anything to say: it reads
        // per-volume ISBNs, which have nothing to do with whether this series is linked.
        // Ordered after the count so a volume the count just created can be enriched in
        // the same run.
        if (info is not null)
        {
            ApplyVolumeCount(series, info, run);
        }

        await EnrichVolumesAsync(series, run, cancellationToken);

        if (info is null)
        {
            return;
        }

        // Metadata only. Nothing in this method touches SeriesUserState or
        // VolumeUserState — sync never writes user state (CLAUDE.md).
        series.LastSyncedAt = timeProvider.GetUtcNow();
    }

    private async Task EnrichVolumesAsync(Series series, SyncRun run, CancellationToken cancellationToken)
    {
        if (_lookupBudget <= 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);

        _lookupBudget -= await volumeEnrichment.EnrichAsync(
            series, run, today, _lookupBudget, cancellationToken);
    }

    private async Task<ReleaseSeriesInfo?> LinkAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        CancellationToken cancellationToken)
    {
        var match = await releaseSourceMatcher.MatchAsync(series, linkedExternalIds, cancellationToken);

        if (!match.IsLinked)
        {
            // Recorded rather than retried silently: an unmatched series is a
            // question for a person, and this row is how it gets asked.
            run.Discrepancies.Add(new SyncDiscrepancy
            {
                Id = Guid.CreateVersion7(),
                SeriesId = series.Id,
                SeriesTitle = series.TitleEnglish,
                Field = SyncDiscrepancyField.SeriesLink,
                ResolvedWith = null,
                Note = $"{match.Outcome}: {match.Explanation}",
            }
                .Says(MetadataSourceName.AniList, series.TitleEnglish)
                .Says(
                    MetadataSourceName.MangaUpdates,
                    match.Score is { } score ? $"best match scored {score}" : null));

            return null;
        }

        series.MangaUpdatesId = match.MatchedExternalId;
        linkedExternalIds.Add(match.MatchedExternalId!.Value);
        run.SeriesLinked++;

        return match.MatchedSeries;
    }

    /// <summary>
    /// Applies the release source's volume count: marks those volumes published,
    /// creates any the shelf is missing, and records a disagreement if the counts
    /// differ.
    /// </summary>
    // The conflict rule (CLAUDE.md) gives release dates to MangaUpdates and
    // everything else to AniList, so a volume-count disagreement is recorded and
    // resolved in AniList's favour rather than acted on — nothing is deleted.
    private void ApplyVolumeCount(Series series, ReleaseSeriesInfo info, SyncRun run)
    {
        if (info.VolumeCount is not { } sourceCount)
        {
            return;
        }

        MarkReleased(series, sourceCount, run);

        var trackedCount = series.Volumes.Count;
        if (sourceCount == trackedCount)
        {
            return;
        }

        run.Discrepancies.Add(new SyncDiscrepancy
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            SeriesTitle = series.TitleEnglish,
            Field = SyncDiscrepancyField.VolumeCount,
            ResolvedWith = MetadataSourceName.AniList,
            Note = $"MangaUpdates status: {info.RawStatus}",
        }
            .Says(MetadataSourceName.AniList, trackedCount.ToString(CultureInfo.InvariantCulture))
            .Says(MetadataSourceName.MangaUpdates, sourceCount.ToString(CultureInfo.InvariantCulture)));

        if (sourceCount <= trackedCount)
        {
            return;
        }

        var highestTracked = series.Volumes.Count == 0
            ? 0
            : series.Volumes.Max(volume => volume.Number);

        for (var number = highestTracked + 1; number <= sourceCount; number++)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // No date, because the release source has none to give. The volume
                // exists; when it came out is not something either source knows.
                ReleaseDate = null,

                // Sync created it, so sync owns it.
                IsLocalOnly = false,
            });

            run.NewVolumes++;
        }

        // The volumes just created are within the source's count, so they are out too.
        MarkReleased(series, sourceCount, run);
    }

    /// <summary>
    /// Marks volumes 1..<paramref name="sourceCount"/> as published, because that is
    /// what a release source's count means.
    /// </summary>
    // This is what stands in for release dates. It is not a date and does not pretend
    // to be one: it records that the source says these volumes exist in print, which
    // is exactly what "to buy" needs to know and all that MangaUpdates will tell us.
    // Volumes beyond the count are left alone — Overlord's 17 and 18 are tracked from
    // AniList but MangaUpdates counts 16, so 17 and 18 are not to-buy.
    private void MarkReleased(Series series, int sourceCount, SyncRun run)
    {
        foreach (var volume in series.Volumes)
        {
            if (volume.Number <= sourceCount && !volume.IsReleased)
            {
                volume.IsReleased = true;
                run.VolumesMarkedReleased++;
            }
        }
    }

    /// <summary>
    /// The line the sidebar prints. Transparent, not magical (design.md's voice
    /// rules) — including about what this sync cannot do.
    /// </summary>
    private string WriteSummary(SyncRun run)
    {
        var parts = new List<string>
        {
            $"{run.SeriesRefreshed} series refreshed",
        };

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

        if (run.VolumeCoversCached > 0)
        {
            parts.Add($"{run.VolumeCoversCached} volume covers cached");
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
        // count, dates only reach the volumes whose ISBN is known, and the footer should
        // not imply the shelf knows more than that.
        parts.Add($"{releaseSource.SourceName} counts only; dates where an ISBN is known");

        return string.Join(" · ", parts);
    }
}
