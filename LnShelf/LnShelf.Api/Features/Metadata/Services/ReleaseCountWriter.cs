namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Applies the release source's volume count: creates any volumes the shelf is missing,
/// marks the counted ones published, and records a disagreement if the counts differ.
/// </summary>
// Extracted from SeriesRefresher, whose backlog note said the next edit to it extracts rather
// than adds. Letting the user pick a source on a volume-count or release-date disagreement
// was that edit: the count is where both are decided.
//
// The conflict rule (CLAUDE.md) gives volume counts to MangaUpdates, so a source counting
// *more* than the shelf tracks is believed and the missing volumes are created. A source
// counting fewer is recorded and not acted on — nothing is ever deleted — which leaves that
// disagreement resolved in the shelf's favour. A SourceChoice replaces that answer for the
// one field it names; this class reads choices and never writes them.
public sealed class ReleaseCountWriter(IReleaseSource releaseSource, ILogger<ReleaseCountWriter> logger)
{
    public void Apply(Series series, ReleaseSeriesInfo info, SyncRun run, bool refreshedBefore)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(run);

        if (info.VolumeCount is not { } sourceCount)
        {
            return;
        }

        // Written on every refresh, before anything below can act on it: undoing a choice
        // later needs the count as it stood, and nothing else on the shelf records it.
        series.ReleaseSourceVolumeCount = sourceCount;

        // Its provenance, beside it and nowhere else: these describe exactly this number, so
        // they are written whenever it is, choice or no choice.
        series.VolumeCountBasis = info.CountBasis;
        series.VolumeCountEvidence = info.CountEvidence;

        // Main volumes only. The release source counts "9 Volumes + 1 Extra" as nine, so a
        // tracked bonus Vol. 7.5 is not a tenth volume to disagree about — and "13+2" as
        // thirteen, so Solo Leveling's whole 14 and 15 are not either. Only as many extras as
        // the source itself states: a whole volume past them is still a disagreement.
        var trackedCount = series.Volumes.Count(volume => !volume.Number.IsBonusVolume)
            - ExtraVolumes.CountWholeExtrasWithin(series, sourceCount, info.ExtraVolumeCount);
        var countChoice = series.ChoiceFor(SyncDiscrepancyField.VolumeCount);

        if (sourceCount != trackedCount)
        {
            run.Discrepancies.Add(
                SyncDiscrepancies.VolumeCount(series, info, sourceCount, trackedCount, countChoice));
        }

        if (countChoice is { Source: MetadataSourceName.AniList })
        {
            // The shelf's own list stands as the count: no volume is created, and every
            // synced one counts as out. A choice is the user's act, so it never raises the
            // new-volume badge either.
            MarkReleasedThrough(series, series.Volumes.Max(volume => (decimal?)volume.Number) ?? 0, run, includeLocalOnly: false);
            return;
        }

        // Every missing number up to the count, not only the ones above the highest tracked:
        // an import adds just the volumes in the export, and a shelf holding 2, 3 and 5 of a
        // ten-volume series used to get 6 to 10 and never 1 or 4 (audit 2026-10-09).
        CreateMissingVolumes(series, run, sourceCount, trackedCount);

        // Once, and after any new volumes exist, so the ones just created are covered by
        // the same pass rather than by a second identical loop.
        var newlyReleased = MarkReleasedThrough(series, sourceCount, run, includeLocalOnly: true);

        if (refreshedBefore && newlyReleased > 0)
        {
            FlagNewVolume(series);
        }
    }

    /// <summary>
    /// Marks volumes 1..<paramref name="count"/> as published, because that is what a release
    /// source's count means.
    /// </summary>
    /// <param name="run">Counted into when given; a choice made by the user passes none.</param>
    /// <param name="includeLocalOnly">
    /// False when the count is the shelf's own list rather than a source's: a volume the user
    /// added by hand is not something that list says is in print.
    /// </param>
    /// <returns>How many volumes this call flipped, which is how many are newly in print.</returns>
    // This is what stands in for release dates on the volumes no ISBN reaches. It is not a
    // date and does not pretend to be one: it records that the source says these volumes
    // exist in print, which is exactly what "to buy" needs to know. Volumes beyond the
    // count are left alone — Overlord's 17 and 18 are tracked from AniList but MangaUpdates
    // counts 16, so 17 and 18 are not to-buy.
    //
    // A volume whose date the user took from a store or a library is skipped: that date, not
    // the count, decides whether it is out, so marking it would put a future date on a volume
    // the shelf calls published — the contradiction the choice exists to settle.
    public static int MarkReleasedThrough(Series series, decimal count, SyncRun? run, bool includeLocalOnly)
    {
        ArgumentNullException.ThrowIfNull(series);

        var newlyReleased = 0;

        foreach (var volume in series.Volumes)
        {
            if (volume.Number > count
                || volume.IsReleased
                || (volume.IsLocalOnly && !includeLocalOnly)
                || series.HasDateSourceChoice(volume.Number))
            {
                continue;
            }

            volume.IsReleased = true;
            newlyReleased++;

            if (run is not null)
            {
                run.VolumesMarkedReleased++;
            }
        }

        return newlyReleased;
    }

    /// <summary>
    /// Records, every run, the volumes whose date the user took from a per-volume source while
    /// the count says they are out.
    /// </summary>
    // Here rather than in the date writer because this is the one that knows the count, and
    // because a source that is not asked again (Open Library, once a volume has no gaps)
    // would otherwise never re-record a disagreement that is still standing. Called after the
    // enrichment, so it reads the dates as that run left them. A date that has since passed
    // agrees with the count and is not a disagreement any more.
    public static void RecordChosenDateConflicts(Series series, ReleaseSeriesInfo info, SyncRun run, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(run);

        if (info.VolumeCount is not { } sourceCount)
        {
            return;
        }

        foreach (var choice in series.SourceChoices.Where(candidate =>
                     candidate is { Field: SyncDiscrepancyField.ReleaseDate, VolumeNumber: not null }
                     && candidate.Source is not MetadataSourceName.MangaUpdates))
        {
            var volume = series.Volumes.FirstOrDefault(candidate => candidate.Number == choice.VolumeNumber);

            if (volume is not { ReleaseDate: { } date } || volume.Number > sourceCount || date <= today)
            {
                continue;
            }

            run.Discrepancies.Add(SyncDiscrepancies.ReleaseDate(
                series,
                volume,
                choice.Source,
                date,
                $"You chose {choice.Source}'s date. The count says this volume is out; the chosen date decides.",
                choice));
        }
    }

    private void CreateMissingVolumes(Series series, SyncRun run, int sourceCount, int trackedCount)
    {
        var missingNumbers = Enumerable.Range(1, sourceCount)
            .Select(number => (decimal)number)
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
