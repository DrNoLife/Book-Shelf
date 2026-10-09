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

        // Read before anything below writes it. A series' *first* refresh is the one that
        // runs the moment it is added, and its whole backlist arriving then is not news —
        // see FlagNewVolume.
        var refreshedBefore = series.LastSyncedAt is not null;

        var info = series.MangaUpdatesId is { } externalId
            ? await releaseSource.GetSeriesAsync(externalId, cancellationToken)
            : await LinkAsync(series, run, linkedExternalIds, cancellationToken);

        if (info is not null)
        {
            ApplyVolumeCount(series, info, run, refreshedBefore);
            ApplyPublicationStatus(series, info, run);
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

        // Metadata. The single exception in this class is HasNewVolume (see FlagNewVolume);
        // no other SeriesUserState or VolumeUserState field is ever written here, because
        // sync does not own user state (CLAUDE.md).
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

        var today = timeProvider.Today();

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
    //
    // Self-extinguishing only because an answer of "no synopsis" is written down too, as the
    // empty string — the same three states Volume.Title uses: null is "never asked", empty is
    // "asked, and AniList has none". Left null, a series AniList carries no synopsis for would
    // be asked about every night forever. The cost is that a synopsis somebody adds to AniList
    // later does not arrive on its own; clearing the column to null asks again. Every reader
    // treats blank as absent (SeriesDetailProjection's NullIfBlank), so nothing displays it.
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
                // A failure throws and never reaches here, so this is AniList answering — with
                // no synopsis, or with no such series — and the answer is final for this row.
                series.Description = string.Empty;
                return;
            }

            series.Description = description;

            logger.LogInformation(
                "{TitleEnglish}: description filled in from AniList {AniListId}",
                series.TitleEnglish,
                aniListId);
        }
        // The caller's cancellation only: a timed-out request is a cancelled task nobody
        // asked for, and it is exactly the failure this catch exists to absorb.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // This file's failure philosophy, applied one level down: a refresh that got the
            // volume counts right is not worth failing because a synopsis could not be
            // fetched. The next run will try again — the column is still null, so the guard is
            // still true.
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
            run.Discrepancies.Add(SyncDiscrepancies.SeriesLink(series, match));

            return null;
        }

        series.MangaUpdatesId = match.MatchedExternalId;
        linkedExternalIds.Add(match.MatchedExternalId!.Value);
        run.SeriesLinked++;

        return match.MatchedSeries;
    }

    /// <summary>
    /// Applies the release source's volume count: creates any volumes the shelf is
    /// missing, marks the counted ones published, and records a disagreement if the
    /// counts differ.
    /// </summary>
    // The conflict rule (CLAUDE.md) gives volume counts to MangaUpdates, so a source
    // counting *more* than the shelf tracks is believed and the missing volumes are
    // created. A source counting fewer is recorded and not acted on — nothing is ever
    // deleted — which leaves that disagreement resolved in the shelf's favour.
    private void ApplyVolumeCount(Series series, ReleaseSeriesInfo info, SyncRun run, bool refreshedBefore)
    {
        if (info.VolumeCount is not { } sourceCount)
        {
            return;
        }

        var trackedCount = series.Volumes.Count;

        if (sourceCount != trackedCount)
        {
            run.Discrepancies.Add(SyncDiscrepancies.VolumeCount(series, info, sourceCount, trackedCount));
        }

        // Every missing number up to the count, not only the ones above the highest tracked:
        // an import adds just the volumes in the export, and a shelf holding 2, 3 and 5 of a
        // ten-volume series used to get 6 to 10 and never 1 or 4 (audit 2026-10-09).
        CreateMissingVolumes(series, run, sourceCount, trackedCount);

        // Once, and after any new volumes exist, so the ones just created are covered by
        // the same pass rather than by a second identical loop.
        var newlyReleased = MarkReleased(series, sourceCount, run);

        if (refreshedBefore && newlyReleased > 0)
        {
            FlagNewVolume(series);
        }
    }

    private void CreateMissingVolumes(Series series, SyncRun run, int sourceCount, int trackedCount)
    {
        var missingNumbers = Enumerable.Range(1, sourceCount)
            .Except(series.Volumes.Select(volume => volume.Number))
            .ToList();

        if (missingNumbers.Count == 0)
        {
            return;
        }

        foreach (var number in missingNumbers)
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
    }

    /// <summary>
    /// Applies the release source's word on whether publication has finished.
    /// </summary>
    // Publication status belongs to MangaUpdates by the precedence list (CLAUDE.md), and
    // until this existed it was written once at add time and never again — a series that
    // finished publishing afterwards stayed Ongoing forever.
    //
    // Only the "finished" direction is written. The source's answer is a boolean, and a
    // false cannot tell Ongoing from Hiatus from Cancelled; overwriting AniList's richer
    // value with a guess would lose information the shelf already had. So a disagreement
    // in that direction is recorded for a person and nothing is written.
    private void ApplyPublicationStatus(Series series, ReleaseSeriesInfo info, SyncRun run)
    {
        if (info.IsCompleted)
        {
            if (series.PublicationStatus is PublicationStatus.Completed)
            {
                return;
            }

            logger.LogInformation(
                "{TitleEnglish}: {SourceName} says publication has finished; {PreviousStatus} becomes Completed",
                series.TitleEnglish,
                releaseSource.SourceName,
                series.PublicationStatus);

            series.PublicationStatus = PublicationStatus.Completed;
            return;
        }

        if (series.PublicationStatus is not PublicationStatus.Completed)
        {
            return;
        }

        run.Discrepancies.Add(SyncDiscrepancies.Status(series, info));
    }

    /// <summary>
    /// Marks volumes 1..<paramref name="sourceCount"/> as published, because that is
    /// what a release source's count means.
    /// </summary>
    /// <returns>How many volumes this call flipped, which is how many are newly in print.</returns>
    // This is what stands in for release dates on the volumes no ISBN reaches. It is not a
    // date and does not pretend to be one: it records that the source says these volumes
    // exist in print, which is exactly what "to buy" needs to know. Volumes beyond the
    // count are left alone — Overlord's 17 and 18 are tracked from AniList but MangaUpdates
    // counts 16, so 17 and 18 are not to-buy.
    private static int MarkReleased(Series series, int sourceCount, SyncRun run)
    {
        var newlyReleased = 0;

        foreach (var volume in series.Volumes)
        {
            if (volume.Number <= sourceCount && !volume.IsReleased)
            {
                volume.IsReleased = true;
                run.VolumesMarkedReleased++;
                newlyReleased++;
            }
        }

        return newlyReleased;
    }

    /// <summary>
    /// Raises the badge that says a volume the reader has not seen yet is out.
    /// </summary>
    // The one place sync writes user state, and CLAUDE.md sanctions exactly this field:
    // HasNewVolume is last-visit acknowledgment — an event — rather than something derivable
    // from read, owned and released, and sync is the only thing that witnesses the event.
    // Nothing else on the row is touched, and it is never cleared here: clearing is the
    // reader's act, and the Shelf slice owns it.
    //
    // Only when the row already exists. Creating one is the Shelf slice's job, and a series
    // with no user state has nobody tracking it to notify.
    private static void FlagNewVolume(Series series)
    {
        if (series.UserState is { } userState)
        {
            userState.HasNewVolume = true;
        }
    }
}
