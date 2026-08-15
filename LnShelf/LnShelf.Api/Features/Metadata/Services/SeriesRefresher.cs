namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Refreshes one series against the release source and the enrichment source: links it,
/// applies the volume count, and enriches whatever volumes carry an ISBN.
/// </summary>
// Extracted from SeriesSyncService when adding a series started needing the same work.
// The nightly sweep and a single on-demand refresh must do *identically* the same thing to
// one series — feature-spec §4.2 says "same code path" about exactly this — and the way to
// guarantee that is for there to be only one copy of it.
//
// It deliberately does not create or save a SyncRun. Its caller owns the run, because
// "what a run is" differs: the sweep is every series, an on-demand refresh is one.
public sealed class SeriesRefresher(
    IReleaseSourceMatcher releaseSourceMatcher,
    IReleaseSource releaseSource,
    ISeriesMetadataSource metadataSource,
    VolumeEnrichmentService volumeEnrichment,
    TimeProvider timeProvider,
    ILogger<SeriesRefresher> logger)
{
    /// <param name="linkedExternalIds">
    /// Release-source ids already spoken for, so two series cannot link to one record.
    /// Added to as links are made.
    /// </param>
    public async Task RefreshAsync(
        Series series,
        SyncRun run,
        HashSet<long> linkedExternalIds,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(linkedExternalIds);
        ArgumentNullException.ThrowIfNull(budget);

        var info = series.MangaUpdatesId is { } externalId
            ? await releaseSource.GetSeriesAsync(externalId, cancellationToken)
            : await LinkAsync(series, run, linkedExternalIds, cancellationToken);

        if (info is not null)
        {
            ApplyVolumeCount(series, info, run);
        }

        // Enrichment runs whether or not the release source had anything to say: it reads
        // per-volume ISBNs, which have nothing to do with whether this series is linked.
        // Ordered after the count so a volume the count just created can be enriched in
        // the same pass.
        await EnrichVolumesAsync(series, run, budget, cancellationToken);

        // Also unconditional, and for the same reason: whether this series has a synopsis
        // has nothing to do with whether the release source recognised it.
        await HealDescriptionAsync(series, cancellationToken);

        if (info is null)
        {
            return;
        }

        // Metadata only. Nothing in this class touches SeriesUserState or VolumeUserState —
        // sync never writes user state (CLAUDE.md).
        series.LastSyncedAt = timeProvider.GetUtcNow();
    }

    private async Task EnrichVolumesAsync(
        Series series,
        SyncRun run,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        if (budget.IsExhausted)
        {
            return;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);

        // The budget is handed over rather than a number: one volume can cost several
        // requests when its own ISBN is not the one that got indexed, and only the
        // enrichment knows how many it actually spent.
        await volumeEnrichment.EnrichAsync(series, run, today, budget, cancellationToken);
    }

    /// <summary>
    /// Gives a series a synopsis when it has none — the series added before the shelf
    /// ever asked AniList for one.
    /// </summary>
    // The description only, never the rest of the metadata. Re-applying every AniList field
    // here would make a gap-fill indistinguishable from a full re-sync, and the other fields
    // were already written when the series was added.
    //
    // No budget mechanics, deliberately, unlike the enrichment above. This costs one AniList
    // request per description-less series and is self-extinguishing — the guard stops firing
    // the moment the column is filled. AniList allows 90 requests a minute and this shelf
    // holds single digits of series, so the worst case is one sweep spending nine of them,
    // once. A LookupBudget here would be machinery guarding nothing.
    private async Task HealDescriptionAsync(Series series, CancellationToken cancellationToken)
    {
        if (series.Description is not null || series.AniListId is not { } aniListId)
        {
            return;
        }

        try
        {
            var metadata = await metadataSource.GetSeriesAsync(aniListId, cancellationToken);

            if (metadata?.Description is not { } description)
            {
                return;
            }

            series.Description = description;

            logger.LogInformation(
                "{TitleEnglish}: description filled in from AniList {AniListId}",
                series.TitleEnglish,
                aniListId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // This file's failure philosophy, applied one level down: a refresh that got the
            // volume counts right is not worth failing because a synopsis could not be
            // fetched. The next run will try again — the guard is still true.
            logger.LogWarning(
                exception,
                "Could not fetch a description for {TitleEnglish} (AniList {AniListId}); continuing",
                series.TitleEnglish,
                aniListId);
        }
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
    // The conflict rule (CLAUDE.md) gives volume counts to MangaUpdates and everything
    // else to AniList, so a volume-count disagreement is recorded and resolved in
    // AniList's favour rather than acted on — nothing is deleted.
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

        logger.LogInformation(
            "{TitleEnglish}: {SourceName} counts {SourceCount} volumes against {TrackedCount} tracked",
            series.TitleEnglish,
            releaseSource.SourceName,
            sourceCount,
            trackedCount);

        // The volumes just created are within the source's count, so they are out too.
        MarkReleased(series, sourceCount, run);
    }

    /// <summary>
    /// Marks volumes 1..<paramref name="sourceCount"/> as published, because that is
    /// what a release source's count means.
    /// </summary>
    // This is what stands in for release dates on the volumes no ISBN reaches. It is not a
    // date and does not pretend to be one: it records that the source says these volumes
    // exist in print, which is exactly what "to buy" needs to know. Volumes beyond the
    // count are left alone — Overlord's 17 and 18 are tracked from AniList but MangaUpdates
    // counts 16, so 17 and 18 are not to-buy.
    private static void MarkReleased(Series series, int sourceCount, SyncRun run)
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
}
