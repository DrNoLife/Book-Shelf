using LnShelf.Shared.Activity;

namespace LnShelf.Api.Features.Activity.Mappers;

/// <summary>
/// Turns the shelf events since the last visit into the panel's per-series groups.
/// </summary>
public static class ActivityGrouping
{
    /// <summary>The most events the panel shows. It is a glance, not a log.</summary>
    public const int MaximumEvents = 30;

    public static ActivityResponse Group(IEnumerable<ActivityEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var all = entries.ToList();

        // One line per volume, the newest thing that happened to it. A volume dated, then
        // moved, then out within one absence is news once: "Vol 9 is out", not three lines
        // of history the reader no longer needs.
        var newest = all
            .GroupBy(entry => (entry.SeriesId, entry.VolumeNumber))
            .Select(Collapse)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenBy(entry => entry.VolumeNumber)
            .Take(MaximumEvents)
            .ToList();

        // GroupBy keeps the order in which each key first appears, so the series with the
        // newest event leads and each series' events stay newest first.
        return new ActivityResponse
        {
            Series =
            [
                .. newest
                    .GroupBy(entry => entry.SeriesId)
                    .Select(series => new ActivitySeriesResponse
                    {
                        SeriesId = series.Key,
                        SeriesTitle = series.First().SeriesTitle,
                        Format = series.First().Format,
                        Events = [.. series.Select(ToEvent)],
                    }),
            ],

            // Over everything read, not just what survived the cap: "Got it" acknowledges
            // the older lines the panel left out as well.
            Through = all.Count > 0 ? all.Max(entry => entry.OccurredAt) : null,
        };
    }

    private static ActivityEntry Collapse(IEnumerable<ActivityEntry> volume)
    {
        var ordered = volume.OrderBy(entry => entry.OccurredAt).ToList();
        var oldest = ordered[0];
        var newestEntry = ordered[^1];

        // Dated, then moved, within one absence: the reader never saw the first date, so the
        // news is "dated 12 Dec", not "moved to 12 Dec". The date they last knew is the one
        // before the oldest change.
        if (newestEntry.Kind is ShelfEventKind.ReleaseDateChanged && oldest.Kind is ShelfEventKind.ReleaseDateChanged)
        {
            return newestEntry with { PreviousDate = oldest.PreviousDate };
        }

        return newestEntry;
    }

    private static ActivityEventResponse ToEvent(ActivityEntry entry) => new()
    {
        Kind = entry.Kind,
        VolumeNumber = entry.VolumeNumber,
        PreviousDate = entry.PreviousDate,
        NewDate = entry.NewDate,
        OccurredAt = entry.OccurredAt,
    };
}
