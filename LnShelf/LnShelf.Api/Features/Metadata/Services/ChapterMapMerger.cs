namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Merges every usable map into one range per volume: precedence picks, the order is checked
/// across sources, ends are closed from the merged starts, and disagreements are named. Pure.
/// </summary>
// ChapterMapBuilder judges each source alone, so each map is rising on its own; what can go
// wrong is only where two sources meet. Three rules, in this order:
//
//   1. Precedence, checked. A volume takes the first source's range whose start rises above the
//      volume before it as merged; a range that does not falls back to the next source's, or the
//      volume goes unranged. Greedy from volume 1 up: a later volume that restarts the numbering
//      (Berserk's list restarts at "001." after four prologue volumes) gives way to the source
//      whose numbering the earlier volumes already follow.
//   2. Ends from the merged starts. A volume followed by a ranged next volume ends at the larger
//      of its own highest chapter below that start and the whole chapter just before it — so a
//      source whose list misses the volume's last chapter or two still ends it in the right
//      place, and a volume ending on an extra (16.5 before 17) keeps it. Before a gap, its own
//      source's end stands only while it stays below the next ranged start.
//   3. A start two sources put more than one chapter apart is a disagreement to record (CLAUDE.md,
//      recorded, never silently picked). One apart is numbering style (a prologue as chapter 0),
//      not a dispute.
public static class ChapterMapMerger
{
    private const decimal DisagreementThreshold = 1m;

    /// <param name="maps">Usable maps in source precedence order.</param>
    public static MergedChapterMap Merge(IReadOnlyList<(MetadataSourceName Source, ChapterMap Map)> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);

        var numbers = maps.SelectMany(entry => entry.Map.Ranges).Select(range => range.VolumeNumber).Distinct().Order();
        var chosen = new List<Choice>();
        var disagreements = new List<ChapterStartDisagreement>();

        foreach (var number in numbers)
        {
            var offers = Offers(maps, number);
            var previousStart = chosen.Count > 0 ? chosen[^1].Range.FirstChapter : (decimal?)null;
            var pick = offers.FirstOrDefault(offer => previousStart is not { } start || offer.Range.FirstChapter > start);

            if (pick is not null)
            {
                chosen.Add(pick);
            }

            if (offers.Count > 1
                && offers.Max(offer => offer.Range.FirstChapter) - offers.Min(offer => offer.Range.FirstChapter) > DisagreementThreshold)
            {
                disagreements.Add(new ChapterStartDisagreement(
                    number,
                    [.. offers.Select(offer => new SourcedVolumeRange(offer.Source, offer.Range))],
                    pick?.Source));
            }
        }

        return new MergedChapterMap([.. chosen.Select((choice, index) => Closed(choice, index, chosen))], disagreements);
    }

    private static List<Choice> Offers(IReadOnlyList<(MetadataSourceName Source, ChapterMap Map)> maps, int number) =>
    [
        .. maps
            .Select(entry => (entry.Source, entry.Map, Range: entry.Map.Ranges.FirstOrDefault(range => range.VolumeNumber == number)))
            .Where(entry => entry.Range is not null)
            .Select(entry => new Choice(entry.Source, entry.Range!, entry.Map.Chapters.GetValueOrDefault(number) ?? [entry.Range!.FirstChapter])),
    ];

    private static MergedVolumeRange Closed(Choice choice, int index, List<Choice> chosen)
    {
        var range = choice.Range;
        var next = index < chosen.Count - 1 ? chosen[index + 1] : null;

        if (next is null)
        {
            return new MergedVolumeRange(range, choice.Source);
        }

        var nextStart = next.Range.FirstChapter;

        decimal? last = next.Range.VolumeNumber == range.VolumeNumber + 1
            ? Math.Max(choice.Chapters.Where(chapter => chapter < nextStart).Max(), decimal.Ceiling(nextStart) - 1)
            : range.LastChapter < nextStart ? range.LastChapter : null;

        return new MergedVolumeRange(range with { LastChapter = last }, choice.Source);
    }

    /// <summary>One source's range for one volume, with the chapters it was built from.</summary>
    private sealed record Choice(MetadataSourceName Source, VolumeChapterRange Range, IReadOnlyList<decimal> Chapters);
}
