using LnShelf.Api.Features.Metadata.Models.MangaDex;

namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Two MangaDex aggregates — English only, and every language — into the source-neutral
/// "whole volume to chapters" the chapter-map builder reads.
/// </summary>
// Per volume, the English aggregate wins where it has the volume at all: the volume breaks the
// shelf tracks are the English edition's, and the all-languages aggregate is where scanlation
// groups disagree (Frieren's vol 4 overlaps vols 3 and 5 there). The all-languages one fills
// the volumes English lacks (Chainsaw Man's English stops at vol 11) and extends an English
// volume past its last English chapter, because a translation that stopped mid-volume says
// nothing about where the volume ends (Slime's English vol 24 has only ch 106).
//
// Either way an all-languages chapter is taken only inside English's breaks: below the start of
// the next volume English has, and above English's own last chapter of the volume it extends
// (or the start of the English volume before one English lacks). Some groups number
// differently — Berserk's count its prologue as 1–16 where English says 0.01–0.16 — and their
// chapters must never lower a start or cross English's breaks. Starts and a volume's own last
// chapter rather than every English chapter, because English files strays too (Chainsaw Man's
// ch 232 under vol 1) and one would wall off the rest of the series.
//
// Only whole volume numbers of 1 and up are kept. "none" is MangaDex's bucket for unassigned
// chapters, "0" holds prologues and one-shots, and a decimal volume is nobody's edition. A
// chapter filed both under a volume and under "none" therefore keeps its volume.
public static class MangaDexAggregateMapper
{
    public static IReadOnlyDictionary<int, IReadOnlyList<decimal>> ToChaptersByVolume(
        MangaDexAggregateResponse? english,
        MangaDexAggregateResponse? allLanguages)
    {
        var englishVolumes = Read(english);
        var allVolumes = Read(allLanguages);

        var merged = new Dictionary<int, IReadOnlyList<decimal>>();

        foreach (var volumeNumber in englishVolumes.Keys.Union(allVolumes.Keys))
        {
            var inEnglish = englishVolumes.GetValueOrDefault(volumeNumber) ?? [];
            var additions = allVolumes.GetValueOrDefault(volumeNumber)?
                .Where(chapter => IsBetweenEnglishNeighbours(chapter, volumeNumber, englishVolumes)) ?? [];

            List<decimal> chapters = [.. inEnglish.Union(additions).Order()];

            if (chapters.Count > 0)
            {
                merged[volumeNumber] = chapters;
            }
        }

        return merged;
    }

    /// <summary>
    /// Below the next English volume's start, and above this volume's last English chapter —
    /// or, where English lacks the volume, above the previous English volume's start.
    /// </summary>
    private static bool IsBetweenEnglishNeighbours(
        decimal chapter,
        int volumeNumber,
        Dictionary<int, SortedSet<decimal>> englishVolumes)
    {
        var before = englishVolumes.Keys.Where(number => number < volumeNumber).DefaultIfEmpty(0).Max();
        var after = englishVolumes.Keys.Where(number => number > volumeNumber).DefaultIfEmpty(0).Min();

        decimal? floor = englishVolumes.TryGetValue(volumeNumber, out var own)
            ? own.Max
            : englishVolumes.GetValueOrDefault(before)?.Min;
        var ceiling = englishVolumes.GetValueOrDefault(after)?.Min;

        return (floor is not { } low || chapter > low) && (ceiling is not { } high || chapter < high);
    }

    private static Dictionary<int, SortedSet<decimal>> Read(MangaDexAggregateResponse? aggregate)
    {
        var volumes = new Dictionary<int, SortedSet<decimal>>();

        if (aggregate is null)
        {
            return volumes;
        }

        foreach (var (key, volume) in Entries(aggregate.Volumes))
        {
            var label = volume.ValueKind is JsonValueKind.Object
                        && volume.TryGetProperty("volume", out var named)
                        && named.ValueKind is JsonValueKind.String
                ? named.GetString()
                : key;

            if (WholeVolumeNumber(label) is not { } volumeNumber
                || !volume.TryGetProperty("chapters", out var chapterList))
            {
                continue;
            }

            var chapters = new SortedSet<decimal>();
            foreach (var (_, chapter) in Entries(chapterList))
            {
                if (chapter.ValueKind is JsonValueKind.Object
                    && chapter.TryGetProperty("chapter", out var number)
                    && number.ValueKind is JsonValueKind.String
                    && ChapterNumber(number.GetString()) is { } parsed)
                {
                    chapters.Add(parsed);
                }
            }

            if (chapters.Count > 0)
            {
                volumes[volumeNumber] = chapters;
            }
        }

        return volumes;
    }

    /// <summary>The members of a keyed object or an array, alike; nothing for anything else.</summary>
    private static IEnumerable<(string? Key, JsonElement Value)> Entries(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(property => ((string?)property.Name, property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(item => ((string?)null, item)),
        _ => [],
    };

    private static int? WholeVolumeNumber(string? label) =>
        int.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1
            ? number
            : null;

    // Decimal chapters are real ("12.5", Berserk's "0.01"); anything that is not a number
    // ("Extra", "Oneshot") is skipped rather than guessed at.
    private static decimal? ChapterNumber(string? text) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
