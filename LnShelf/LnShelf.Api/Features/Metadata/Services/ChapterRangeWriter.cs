namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Writes the merged chapter ranges onto a series' existing whole volumes, and clears the ranges
/// no map covers any more.
/// </summary>
// Synced metadata only (CLAUDE.md): a range lands on a volume the shelf already tracks, keyed by
// its whole number. A map never creates a volume, never deletes one, and never touches user
// state — the offer to mark a volume read is computed from these columns on the way out.
public static class ChapterRangeWriter
{
    /// <param name="ranges">One range per volume, from ChapterMapMerger. Empty clears every range.</param>
    /// <returns>How many volumes' ranges changed, for the run's summary.</returns>
    // A range for a volume the shelf does not track is simply not written: a source following
    // the Japanese edition maps volumes the English one has not reached.
    public static int Apply(Series series, IReadOnlyList<MergedVolumeRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(ranges);

        var changed = 0;

        foreach (var volume in series.Volumes)
        {
            var merged = RangeFor(volume, ranges);

            var first = merged?.Range.FirstChapter;
            var last = merged?.Range.LastChapter;
            var rangeSource = merged?.Source;

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
    private static MergedVolumeRange? RangeFor(Volume volume, IReadOnlyList<MergedVolumeRange> ranges) =>
        volume.Number == decimal.Truncate(volume.Number)
            ? ranges.FirstOrDefault(merged => merged.Range.VolumeNumber == volume.Number)
            : null;
}
