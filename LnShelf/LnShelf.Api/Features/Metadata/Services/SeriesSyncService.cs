namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class SeriesSyncService(
    LnShelfDbContext dbContext,
    IReleaseSourceMatcher releaseSourceMatcher,
    IReleaseSource releaseSource,
    TimeProvider timeProvider,
    ILogger<SeriesSyncService> logger) : ISeriesSyncService
{
    public async Task<SyncRun> RunAsync(CancellationToken cancellationToken)
    {
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

        if (info is null)
        {
            return;
        }

        ApplyVolumeCount(series, info, run);

        // Metadata only. Nothing in this method touches SeriesUserState or
        // VolumeUserState — sync never writes user state (CLAUDE.md).
        series.LastSyncedAt = timeProvider.GetUtcNow();
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
                AniListValue = series.TitleEnglish,
                MangaUpdatesValue = match.Score is { } score ? $"best match scored {score}" : null,
                ResolvedWith = null,
                Note = $"{match.Outcome}: {match.Explanation}",
            });

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
            AniListValue = trackedCount.ToString(CultureInfo.InvariantCulture),
            MangaUpdatesValue = sourceCount.ToString(CultureInfo.InvariantCulture),
            ResolvedWith = MetadataSourceName.AniList,
            Note = $"MangaUpdates status: {info.RawStatus}",
        });

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

        if (run.Discrepancies.Count > 0)
        {
            parts.Add($"{run.Discrepancies.Count} to review");
        }

        if (run.Failures > 0)
        {
            parts.Add($"{run.Failures} failed");
        }

        // Said out loud rather than left as a mystery: the shelf knows what is in
        // print but not when it arrived, and the footer should not imply otherwise.
        parts.Add($"counts only — {releaseSource.SourceName} publishes no dates");

        return string.Join(" · ", parts);
    }
}
