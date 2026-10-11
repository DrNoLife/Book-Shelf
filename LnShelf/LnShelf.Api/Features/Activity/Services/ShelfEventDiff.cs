namespace LnShelf.Api.Features.Activity.Services;

/// <summary>
/// What a refresh changed about a series that had been synced before, as activity-log
/// entries: a snapshot taken before the refresh, compared with the series after it.
/// </summary>
// One diff over the whole refresh rather than events raised by each writer. A volume becomes
// "out" in two ways: the release source's count flips IsReleased, or a dated volume's date
// passes. The projections count both (LibraryCardProjection, SeriesDetailProjection), and the
// date usually passes days before the count catches up, so a flip-based event arrived late
// and then a second time. Comparing "out before" with "out now" sees each volume come out
// once, whichever way it happened.
public static class ShelfEventDiff
{
    /// <returns>Null for a series that has never been synced: its first refresh is not news.</returns>
    public static ShelfSnapshot? Capture(Series series, TimeZoneInfo localZone)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(localZone);

        if (series.LastSyncedAt is not { } lastSyncedAt)
        {
            return null;
        }

        // The local calendar day of the last sync, the same convention TimeProvider.Today()
        // uses: a volume dated on or before that day already counted as out back then.
        var previousSyncDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(lastSyncedAt, localZone).DateTime);

        var volumes = series.Volumes.ToDictionary(
            volume => volume.Id,
            volume => new VolumeSnapshot(volume.Number, volume.IsReleased, volume.ReleaseDate));

        return new ShelfSnapshot(
            volumes,
            series.ReleaseSourceVolumeCount,
            series.Volumes.Max(volume => (decimal?)volume.Number),
            previousSyncDay,
            series.MangaUpdatesId);
    }

    /// <summary>
    /// The events between <paramref name="before"/> and the series as it is now, in volume order.
    /// </summary>
    public static IReadOnlyList<ShelfEvent> Compare(
        ShelfSnapshot before,
        Series after,
        DateOnly today,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        // Linked to a different release-source record during this refresh: the count and the
        // volumes past it now come from a record this series has never been compared with, so
        // the refresh is a first one again. A wrong 3-volume record swapped for the right
        // 14-volume one would otherwise announce volumes 4 to 14 as out today.
        if (after.MangaUpdatesId != before.MangaUpdatesId)
        {
            return [];
        }

        var events = new List<ShelfEvent>();

        foreach (var volume in after.Volumes.OrderBy(volume => volume.Number))
        {
            var kind = before.Volumes.TryGetValue(volume.Id, out var previous)
                ? KindForExisting(previous, volume, before.PreviousSyncDay, today)
                : KindForNew(before, volume, today);

            if (kind is not { } eventKind)
            {
                continue;
            }

            events.Add(new ShelfEvent
            {
                Id = Guid.CreateVersion7(),
                SeriesId = after.Id,
                Kind = eventKind,
                VolumeNumber = volume.Number,
                PreviousDate = eventKind is ShelfEventKind.ReleaseDateChanged ? previous?.ReleaseDate : null,
                NewDate = volume.ReleaseDate,
                OccurredAt = occurredAt,
            });
        }

        return events;
    }

    private static ShelfEventKind? KindForExisting(
        VolumeSnapshot previous,
        Volume volume,
        DateOnly previousSyncDay,
        DateOnly today)
    {
        if (IsOut(previous.IsReleased, previous.ReleaseDate, previousSyncDay))
        {
            // Out already: nothing about it is news any more, including a date a catalogue
            // corrected after the fact.
            return null;
        }

        if (IsOut(volume.IsReleased, volume.ReleaseDate, today))
        {
            return ShelfEventKind.VolumeReleased;
        }

        // Still not out. Only a move to a real future date is worth telling; a date that
        // vanished says nothing a reader can act on. "Not out" already means any date it has
        // is after today.
        return volume.ReleaseDate is not null && volume.ReleaseDate != previous.ReleaseDate
            ? ShelfEventKind.ReleaseDateChanged
            : null;
    }

    // A row the shelf did not have before is news only when it is past the end of what was
    // known. The count fills gaps below the highest tracked number (an import's missing 1 and
    // 4), and the catalogue adds bonus volumes that have been out for years; neither is new to
    // the world, only to this shelf.
    private static ShelfEventKind? KindForNew(ShelfSnapshot before, Volume volume, DateOnly today)
    {
        var knownEnd = Math.Max(before.ReleaseSourceVolumeCount ?? 0, before.HighestNumber ?? 0);
        if (volume.Number <= knownEnd)
        {
            return null;
        }

        return IsOut(volume.IsReleased, volume.ReleaseDate, today)
            ? ShelfEventKind.VolumeReleased
            : ShelfEventKind.VolumeAnnounced;
    }

    // The projections' own rule for "out": the release source says so, or its date has come.
    private static bool IsOut(bool isReleased, DateOnly? releaseDate, DateOnly onDay) =>
        isReleased || (releaseDate is { } date && date <= onDay);
}
