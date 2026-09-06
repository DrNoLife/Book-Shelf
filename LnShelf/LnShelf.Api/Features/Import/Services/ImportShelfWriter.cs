namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Applies one matched series' imported rows to the shelf. Extracted from the
/// command service because "write a series and its volumes" is its own job, and
/// it is the job with all the rules in it.
/// </summary>
public sealed class ImportShelfWriter(
    LnShelfDbContext dbContext,
    ISeriesMetadataSource metadataSource,
    ICoverCache coverCache,
    ILogger<ImportShelfWriter> logger)
{
    /// <param name="groups">
    /// Every group that resolved to <paramref name="aniListId"/> — one shelf entry
    /// can arrive under two titles.
    /// </param>
    public async Task ApplyAsync(
        int aniListId,
        IReadOnlyList<ImportGroup> groups,
        ImportWriteCounters counters,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(counters);

        var series = await FindOrCreateSeriesAsync(aniListId, counters, appliedAt, cancellationToken);
        var desiredStates = DesiredVolumeStateMerger.Merge(groups);

        foreach (var desired in desiredStates.Values.OrderBy(state => state.VolumeNumber))
        {
            ApplyVolume(series, desired, counters);
        }

        ApplySeriesState(series, desiredStates.Values);

        logger.LogInformation(
            "Applied AniList {AniListId} ({TitleEnglish}): {VolumeCount} volumes from {GroupCount} import group(s)",
            aniListId,
            series.TitleEnglish,
            desiredStates.Count,
            groups.Count);
    }

    private async Task<Series> FindOrCreateSeriesAsync(
        int aniListId,
        ImportWriteCounters counters,
        DateTimeOffset appliedAt,
        CancellationToken cancellationToken)
    {
        // Keyed on AniList id, which is what makes a re-import additive: the second
        // run finds what the first one created and updates it (feature-spec §4.4).
        var existing = await dbContext.Series
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .Include(series => series.UserState)
            .FirstOrDefaultAsync(series => series.AniListId == aniListId, cancellationToken);

        if (existing is not null)
        {
            // Deliberately no metadata refresh. The series' metadata was fetched
            // when it was first added, and re-fetching it on every import would
            // spend an AniList request per series to learn nothing — which is
            // exactly what makes committing the same export twice cost nothing.
            // Keeping metadata current is the nightly sync's job (phase 2).
            counters.SeriesUpdated++;
            return existing;
        }

        var metadata = await metadataSource.GetSeriesAsync(aniListId, cancellationToken)
            ?? throw new ConflictException(
                $"AniList no longer has a series with id {aniListId}; re-run the match for it.");

        var coverObjectKey = await coverCache.EnsureCachedAsync(
            aniListId, metadata.CoverImageUrl, cancellationToken);

        var series = SeriesMetadataMapper.ToTrackedSeries(metadata, appliedAt, coverObjectKey);
        dbContext.Series.Add(series);
        counters.SeriesCreated++;

        return series;
    }

    private static void ApplyVolume(Series series, DesiredVolumeState desired, ImportWriteCounters counters)
    {
        var volume = series.Volumes.FirstOrDefault(candidate => candidate.Number == desired.VolumeNumber);

        if (volume is null)
        {
            // The metadata source does not know this volume — normal for a series
            // AniList has no volume count for at all. Flagged local-only so a later
            // sync reconciles rather than overwrites it (feature-spec §4.1).
            volume = new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = desired.VolumeNumber,
                IsLocalOnly = true,
            };

            series.Volumes.Add(volume);
            counters.LocalOnlyVolumesCreated++;
        }

        // The export's own identifier for the edition, kept because it is the only key
        // per-volume enrichment works by. Never overwritten: a re-import that has no
        // ISBN for this volume must not erase the one already recorded, and a second
        // export naming a different edition is not grounds for replacing the first.
        volume.Isbn ??= desired.Isbn;

        // Fill-if-null, and only with something. The export's title already names the
        // volume ("Overlord, Vol. 3: The Bloody Valkyrie"), which is the same fact the
        // subtitle backfill spends an Open Library lookup per volume to learn — so
        // taking it here is free. It must never overwrite: a synced or enriched title is
        // a source's own answer, and Volume.Title's "" specifically means "a source was
        // asked and this volume has no subtitle", which a silent CSV is no evidence
        // against.
        if (volume.Title is null && desired.Subtitle is not null)
        {
            volume.Title = desired.Subtitle;
            counters.VolumeSubtitlesWritten++;
        }

        volume.UserState ??= new VolumeUserState { VolumeId = volume.Id };
        var state = volume.UserState;

        // Additive, never subtractive: an import raises read status and ownership
        // but never lowers them. Re-importing the same export is then a no-op, and
        // a volume the user has since edited by hand is not quietly reverted.
        if (RankOf(desired.ReadStatus) > RankOf(state.ReadStatus))
        {
            state.ReadStatus = desired.ReadStatus;
        }

        // Earlier wins, rather than fill-if-null. The only date that gets here without
        // an import is the Today() the app stamps when a volume is ticked read, and a
        // stamp that says "today" because that is when the box was ticked must not beat
        // the export's record of when the book was actually read. The rule is two-level:
        // within one import the *latest* read of a volume is its read date
        // (DesiredVolumeStateMerger.Combine), and against what is already on the shelf
        // the *earliest* of the two survives. Neither level ever writes null over a date.
        state.ReadAt = Earliest(state.ReadAt, desired.ReadAt);

        if (desired.Owned && !state.Owned)
        {
            state.Owned = true;
            state.OwnedFormat = desired.OwnedFormat;
        }

        counters.VolumeStatesWritten++;
    }

    /// <summary>
    /// Series-level state from what was imported, without overruling the user.
    /// </summary>
    private static void ApplySeriesState(Series series, IEnumerable<DesiredVolumeState> desiredStates)
    {
        var states = desiredStates.ToList();

        series.UserState ??= new SeriesUserState
        {
            SeriesId = series.Id,
            TrackingStatus = TrackingStatus.PlanToStart,
        };

        var userState = series.UserState;

        // Only ever promoted from the untouched default. Someone who has marked a
        // series Dropped or Completed has said something the importer must not
        // argue with.
        if (userState.TrackingStatus is TrackingStatus.PlanToStart)
        {
            userState.TrackingStatus = ChooseTrackingStatus(series, states);
        }

        var earliest = states.Select(state => state.ReadAt).Where(date => date is not null).Min();
        var latest = states.Select(state => state.ReadAt).Where(date => date is not null).Max();

        // Same rule as the per-volume ReadAt, for the same reason plus one: importing an
        // older export after a newer one could otherwise never move the start date back
        // to where the reading actually began.
        userState.StartedAt = Earliest(userState.StartedAt, earliest);

        if (latest is not null && (userState.LastReadAt is null || latest > userState.LastReadAt))
        {
            userState.LastReadAt = latest;
        }
    }

    /// <summary>The earlier of the two, and never null when either is set.</summary>
    private static DateOnly? Earliest(DateOnly? left, DateOnly? right) =>
        (left, right) switch
        {
            (null, var other) => other,
            (var other, null) => other,
            _ => left < right ? left : right,
        };

    private static TrackingStatus ChooseTrackingStatus(Series series, List<DesiredVolumeState> states)
    {
        if (states.Any(state => state.ReadStatus is ReadStatus.Reading))
        {
            return TrackingStatus.Reading;
        }

        // Completed means the series is finished *and* every volume of it is read.
        // Anything else is Reading, because the import is evidence of reading.
        var everyVolumeRead = series.Volumes.Count > 0
            && series.Volumes.All(volume => volume.UserState?.ReadStatus is ReadStatus.Read);

        return series.PublicationStatus is PublicationStatus.Completed && everyVolumeRead
            ? TrackingStatus.Completed
            : TrackingStatus.Reading;
    }

    private static int RankOf(ReadStatus readStatus) => readStatus switch
    {
        ReadStatus.Read => 2,
        ReadStatus.Reading => 1,
        ReadStatus.Unread => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(readStatus), readStatus, "Unknown read status."),
    };
}
