namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Turns a source's raw "volume to chapters" lists into chapter ranges, or refuses them. Pure:
/// no HTTP, no database, the same answer for the same input.
/// </summary>
// Source-neutral on purpose (IChapterMapSource): every chapter-map source files strays and
// leaves gaps, and these are the shelf's rules for what it will believe. A refusal is the
// normal outcome for a map that does not fit — the shelf then simply shows no ranges.
public static class ChapterMapBuilder
{
    /// <summary>
    /// How far, in typical volumes, a chapter may sit from its volume's middle before it is a stray.
    /// </summary>
    // Two rather than three: Chainsaw Man's vol 20 files ch 203 about 2.3 volumes past its
    // middle (vol 20 is ch 176–186), and three would have kept it.
    private const decimal StrayDistanceInVolumes = 2m;

    /// <summary>A volume averaging more than this many times the shelf's own pace is not the same edition.</summary>
    private const decimal MaximumPaceRatio = 2m;

    /// <summary>The share of released volumes a map must cover to be believed at all.</summary>
    private const decimal MinimumReleasedCoverage = 0.5m;

    public static ChapterMap Build(IReadOnlyDictionary<int, IReadOnlyList<decimal>> chaptersByVolume, ChapterMapShelf shelf)
    {
        ArgumentNullException.ThrowIfNull(chaptersByVolume);
        ArgumentNullException.ThrowIfNull(shelf);

        var volumes = chaptersByVolume
            .Where(entry => entry.Key >= 1 && entry.Value.Count > 0)
            .OrderBy(entry => entry.Key)
            .Select(entry => new VolumeChapters(entry.Key, [.. entry.Value.Distinct().Order()]))
            .ToList();

        if (volumes.Count == 0)
        {
            return ChapterMap.Rejected("The source files no chapters under a volume.");
        }

        var medians = volumes.Select(volume => Median(volume.Chapters)).ToList();
        if (!IsRising(medians))
        {
            return ChapterMap.Rejected("The source's volumes overlap: their middle chapters do not rise.");
        }

        var kept = DropStrays(volumes, medians);
        if (!IsRising([.. kept.Select(volume => volume.Chapters[0])]))
        {
            return ChapterMap.Rejected("The source's volumes do not start on rising chapters.");
        }

        return Refusal(kept, shelf) is { } reason
            ? ChapterMap.Rejected(reason)
            : new ChapterMap { Ranges = ToRanges(kept, shelf.IsCompleted) };
    }

    /// <summary>
    /// Drops what a volume files that belongs elsewhere: far from its own middle, or past a
    /// neighbouring volume's middle.
    /// </summary>
    // A stray is real data in the wrong place — Chainsaw Man files ch 232 under vol 1, Slime
    // ch 138 under vol 4 — and it would otherwise stretch its volume's range across the series.
    // "Typical" is the median step between neighbouring volumes' middles, per volume number, so
    // a gap in the map (vols 21–23 missing) does not read as one enormous volume.
    private static List<VolumeChapters> DropStrays(List<VolumeChapters> volumes, List<decimal> medians)
    {
        List<decimal> steps =
        [
            .. Enumerable.Range(1, volumes.Count - 1)
                .Select(index => (medians[index] - medians[index - 1]) / (volumes[index].Number - volumes[index - 1].Number)),
        ];
        decimal? reach = steps.Count > 0 ? Median(steps) * StrayDistanceInVolumes : null;

        var kept = new List<VolumeChapters>();

        for (var index = 0; index < volumes.Count; index++)
        {
            var median = medians[index];
            decimal? previousMedian = index > 0 ? medians[index - 1] : null;
            decimal? nextMedian = index < volumes.Count - 1 ? medians[index + 1] : null;

            List<decimal> chapters =
            [
                .. volumes[index].Chapters.Where(chapter =>
                    (reach is not { } limit || Math.Abs(chapter - median) <= limit)
                    && (previousMedian is not { } previous || chapter > previous)
                    && (nextMedian is not { } next || chapter < next)),
            ];

            if (chapters.Count > 0)
            {
                kept.Add(new VolumeChapters(volumes[index].Number, chapters));
            }
        }

        return kept;
    }

    /// <returns>Why the map does not fit this shelf, or null when it does.</returns>
    private static string? Refusal(List<VolumeChapters> volumes, ChapterMapShelf shelf)
    {
        if (volumes.Count > shelf.TrackedWholeVolumes + 1)
        {
            return $"The source maps {volumes.Count} volumes; the shelf tracks {shelf.TrackedWholeVolumes}.";
        }

        // Whole chapters only, so a run of 67.1, 67.2 extras does not read as a fat volume.
        // Catches a source whose "volumes" are another edition's (Solo Leveling's seasons).
        if (shelf.LatestChapter is { } latest && shelf.TrackedWholeVolumes > 0)
        {
            var shelfPace = latest / shelf.TrackedWholeVolumes;
            var sourcePace = (decimal)volumes.Sum(volume => volume.Chapters.Count(IsWhole)) / volumes.Count;

            if (sourcePace > MaximumPaceRatio * shelfPace)
            {
                return $"The source's volumes average {sourcePace:0.#} chapters; the shelf's pace is {shelfPace:0.#}.";
            }
        }

        if (shelf.ReleasedWholeVolumes.Count > 0)
        {
            var covered = shelf.ReleasedWholeVolumes.Count(number => volumes.Any(volume => volume.Number == number));

            if ((decimal)covered / shelf.ReleasedWholeVolumes.Count < MinimumReleasedCoverage)
            {
                return $"The source maps {covered} of the {shelf.ReleasedWholeVolumes.Count} volumes that are out.";
            }
        }

        return null;
    }

    /// <summary>
    /// First is a volume's lowest chapter. Last is known only where the next volume's start is,
    /// or for the final volume of a completed series; otherwise the range stays open.
    /// </summary>
    private static List<VolumeChapterRange> ToRanges(List<VolumeChapters> volumes, bool isCompleted)
    {
        var ranges = new List<VolumeChapterRange>();

        for (var index = 0; index < volumes.Count; index++)
        {
            var volume = volumes[index];
            var next = index < volumes.Count - 1 ? volumes[index + 1] : null;

            ranges.Add(new VolumeChapterRange(volume.Number, volume.Chapters[0], LastChapter(volume, next, isCompleted)));
        }

        return ranges;
    }

    // A gap after this volume (the next mapped one is not number + 1) leaves it open: where it
    // ends depends on where the missing volume starts. Never empty below the next start,
    // because starts rise.
    private static decimal? LastChapter(VolumeChapters volume, VolumeChapters? next, bool isCompleted)
    {
        if (next is not null)
        {
            return next.Number == volume.Number + 1
                ? volume.Chapters.Where(chapter => chapter < next.Chapters[0]).Max()
                : null;
        }

        return isCompleted ? volume.Chapters[^1] : null;
    }

    private static bool IsRising(List<decimal> values) =>
        values.Zip(values.Skip(1)).All(pair => pair.Second > pair.First);

    private static bool IsWhole(decimal chapter) => chapter == decimal.Truncate(chapter);

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;

        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    /// <summary>One volume's chapters, sorted ascending, without duplicates.</summary>
    private sealed record VolumeChapters(int Number, List<decimal> Chapters);
}
