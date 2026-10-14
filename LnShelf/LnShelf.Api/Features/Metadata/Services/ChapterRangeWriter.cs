namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Writes chapter ranges onto a series' existing whole volumes, each from the first usable map
/// that covers it, and clears the ranges no map covers any more.
/// </summary>
// Synced metadata only (CLAUDE.md): a range lands on a volume the shelf already tracks, keyed by
// its whole number. A map never creates a volume, never deletes one, and never touches user
// state — the offer to mark a volume read is computed from these columns on the way out.
public static class ChapterRangeWriter
{
    /// <param name="maps">Usable maps in source precedence order. Empty clears every range.</param>
    /// <returns>How many volumes' ranges changed, for the run's summary.</returns>
    public static int Apply(Series series, IReadOnlyList<(MetadataSourceName Source, ChapterMap Map)> maps)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(maps);

        var changed = 0;

        foreach (var volume in series.Volumes)
        {
            var (source, range) = RangeFor(volume, maps);

            var first = range?.FirstChapter;
            var last = range?.LastChapter;
            MetadataSourceName? rangeSource = range is null ? null : source;

            if (volume.FirstChapter == first && volume.LastChapter == last && volume.ChapterRangeSource == rangeSource)
            {
                continue;
            }

            volume.FirstChapter = first;
            volume.LastChapter = last;
            volume.ChapterRangeSource = rangeSource;
            changed++;
        }

        return changed;
    }

    // A bonus volume (7.5) never takes a range: the sources number only whole volumes, and the
    // chapters of 7.5 are not "volume 7".
    private static (MetadataSourceName Source, VolumeChapterRange? Range) RangeFor(
        Volume volume,
        IReadOnlyList<(MetadataSourceName Source, ChapterMap Map)> maps)
    {
        if (volume.Number != decimal.Truncate(volume.Number))
        {
            return (default, null);
        }

        foreach (var (source, map) in maps)
        {
            if (map.Ranges.FirstOrDefault(range => range.VolumeNumber == volume.Number) is { } range)
            {
                return (source, range);
            }
        }

        return (default, null);
    }
}
