using System.Text.RegularExpressions;

namespace LnShelf.Client.Features.Search;

/// <summary>
/// One search hit's title taken apart: the segments its separators divide it into, and the
/// part or season it names, if it names one. What the result grouper compares titles by.
/// </summary>
/// <remarks>
/// "Ascendance of a Bookworm: Part 2 – Apprentice Shrine Maiden" has the stem
/// "Ascendance of a Bookworm", the part "Part 2" and the rest "Apprentice Shrine Maiden"; so
/// has "Ascendance of a Bookworm Part 2: Apprentice Shrine Maiden". A title naming no part is
/// all stem.
/// </remarks>
public sealed partial record SearchTitle
{
    /// <summary>What a remainder is trimmed of at both ends: separators and their spacing.</summary>
    private static readonly char[] SeparatorCharacters = [' ', ':', '-', '–', '—', '~', '～', '〜', '　'];

    private SearchTitle(string original)
    {
        Original = original;
    }

    /// <summary>The title as the source wrote it.</summary>
    public string Original { get; }

    /// <summary>The normalised segments of the whole title, in order.</summary>
    public IReadOnlyList<string> Segments { get; private init; } = [];

    /// <summary>The normalised segments before the part marker; the whole title when there is none.</summary>
    public IReadOnlyList<string> Stem { get; private init; } = [];

    /// <summary>The stem as the source wrote it: "Ascendance of a Bookworm".</summary>
    public string StemText { get; private init; } = string.Empty;

    /// <summary>The first segment as written: "本好きの下剋上" out of a native title with a part in it.</summary>
    public string FirstSegmentText => SegmentEnds.Count == 0 ? Original : TrimSeparators(Original[..SegmentEnds[0]]);

    /// <summary>"Part 2", "Season 3". Null when the title names no part.</summary>
    public string? PartLabel { get; private init; }

    public int? PartNumber { get; private init; }

    /// <summary>What follows the part marker, as written: "Apprentice Shrine Maiden".</summary>
    public string? AfterPart { get; private init; }

    /// <summary>
    /// The words two titles of one work share: the first segment of the stem. "Mushoku Tensei"
    /// for "Mushoku Tensei: Roxy Gets Serious" as much as for "Mushoku Tensei: Jobless
    /// Reincarnation".
    /// </summary>
    // A first segment shorter than three characters is too thin to join works on ("86" alone
    // would claim every title starting with a number), so the whole stem is the key then.
    public string WorkKey
    {
        get
        {
            var words = Stem.Count > 0 ? Stem : Segments;

            if (words.Count == 0)
            {
                return string.Empty;
            }

            return words[0].Length >= 3 ? words[0] : string.Join(' ', words);
        }
    }

    // Where each segment ends in Original, so a remainder can be cut out as the source wrote it.
    private IReadOnlyList<int> SegmentEnds { get; init; } = [];

    public static SearchTitle Parse(string? title)
    {
        var original = title?.Trim() ?? string.Empty;
        var segments = SplitSegments(original);

        for (var index = 0; index < segments.Count; index++)
        {
            var (start, end) = segments[index];

            if (FindPartMarker(original[start..end]) is not { } marker)
            {
                continue;
            }

            var markerStart = start + marker.Index;
            var textBefore = original[start..markerStart];

            List<string> stem = [.. segments.Take(index).Select(segment => SearchTitleNormalizer.Normalize(original[segment.Start..segment.End]))];
            if (SearchTitleNormalizer.Normalize(textBefore) is { Length: > 0 } normalizedBefore)
            {
                stem.Add(normalizedBefore);
            }

            return new SearchTitle(original)
            {
                Segments = NormalizeAll(original, segments),
                SegmentEnds = [.. segments.Select(segment => segment.End)],
                Stem = stem,
                StemText = TrimSeparators(original[..markerStart]),
                PartLabel = $"{marker.Kind} {marker.Number}",
                PartNumber = marker.Number,
                AfterPart = TrimSeparators(original[(markerStart + marker.Length)..]) is { Length: > 0 } after ? after : null,
            };
        }

        var normalized = NormalizeAll(original, segments);

        return new SearchTitle(original)
        {
            Segments = normalized,
            SegmentEnds = [.. segments.Select(segment => segment.End)],
            Stem = normalized,
            StemText = TrimSeparators(original),
        };
    }

    /// <summary>
    /// The title after its first <paramref name="segmentCount"/> segments, as written:
    /// "Roxy Gets Serious" out of "Mushoku Tensei: Roxy Gets Serious" after one. Empty when
    /// nothing is left.
    /// </summary>
    public string RemainderAfter(int segmentCount)
    {
        if (segmentCount <= 0)
        {
            return Original;
        }

        return segmentCount >= SegmentEnds.Count
            ? string.Empty
            : TrimSeparators(Original[SegmentEnds[segmentCount - 1]..]);
    }

    /// <summary>Whether this title's stem begins with every segment of <paramref name="prefix"/>.</summary>
    public bool StemStartsWith(IReadOnlyList<string> prefix) => StartsWith(Stem, prefix);

    /// <summary>Whether this title begins with every segment of <paramref name="prefix"/>.</summary>
    public bool StartsWith(IReadOnlyList<string> prefix) => StartsWith(Segments, prefix);

    private static bool StartsWith(IReadOnlyList<string> segments, IReadOnlyList<string> prefix) =>
        prefix.Count > 0
        && prefix.Count <= segments.Count
        && prefix.Select((segment, index) => segment == segments[index]).All(matches => matches);

    private static List<(int Start, int End)> SplitSegments(string title)
    {
        List<(int Start, int End)> segments = [];
        var start = 0;

        foreach (Match separator in SeparatorPattern().Matches(title))
        {
            AddIfWords(segments, title, start, separator.Index);
            start = separator.Index + separator.Length;
        }

        AddIfWords(segments, title, start, title.Length);

        return segments;
    }

    // A segment with no letters in it ("~" closing a subtitle) separates nothing.
    private static void AddIfWords(List<(int Start, int End)> segments, string title, int start, int end)
    {
        if (end > start && SearchTitleNormalizer.Normalize(title[start..end]).Length > 0)
        {
            segments.Add((start, end));
        }
    }

    private static IReadOnlyList<string> NormalizeAll(string title, List<(int Start, int End)> segments) =>
        [.. segments.Select(segment => SearchTitleNormalizer.Normalize(title[segment.Start..segment.End]))];

    private static string TrimSeparators(string text) => text.Trim(SeparatorCharacters);

    private static PartMarker? FindPartMarker(string segment)
    {
        if (NamedPartPattern().Match(segment) is { Success: true } named)
        {
            return new PartMarker(named.Index, named.Length, KindOf(named.Groups["kind"].Value), ParseNumber(named.Groups["number"].Value));
        }

        if (OrdinalPartPattern().Match(segment) is { Success: true } ordinal)
        {
            return new PartMarker(ordinal.Index, ordinal.Length, KindOf(ordinal.Groups["kind"].Value), int.Parse(ordinal.Groups["number"].Value, CultureInfo.InvariantCulture));
        }

        // Romaji for "part N", the way MangaUpdates files Bookworm: "Dai 2-bu".
        if (RomajiPartPattern().Match(segment) is { Success: true } romaji)
        {
            return new PartMarker(romaji.Index, romaji.Length, "Part", int.Parse(romaji.Groups["number"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    private static string KindOf(string word) => word.ToLowerInvariant() switch
    {
        "season" => "Season",
        "book" => "Book",
        _ => "Part",
    };

    private static int ParseNumber(string number)
    {
        if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var arabic))
        {
            return arabic;
        }

        // Roman numerals, which the pattern only lets through as runs of I, V and X.
        var total = 0;
        var previous = 0;

        foreach (var letter in number.ToLowerInvariant().Reverse())
        {
            var value = letter switch { 'i' => 1, 'v' => 5, _ => 10 };
            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total;
    }

    // ": " with a space (so "Re:ZERO" stays whole), a dash with spaces both sides (so
    // "86—EIGHTY-SIX" stays whole), and the tildes Japanese titles wrap a subtitle in.
    [GeneratedRegex(@"[:：]\s+|\s+[-–—]\s+|\s*[~～〜]\s*")]
    private static partial Regex SeparatorPattern();

    // Roman numerals only in capitals and only after Part or Season: "The Book I Love" is not
    // a second book.
    [GeneratedRegex(@"\b(?<kind>part|season)\s+(?<number>\d+|(?-i:[IVX]+))\b|\b(?<kind>book)\s+(?<number>\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NamedPartPattern();

    [GeneratedRegex(@"\b(?<number>\d+)(?:st|nd|rd|th)\s+(?<kind>season|part)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrdinalPartPattern();

    [GeneratedRegex(@"\bdai\s*(?<number>\d+)\s*-?\s*bu\b", RegexOptions.IgnoreCase)]
    private static partial Regex RomajiPartPattern();

    private sealed record PartMarker(int Index, int Length, string Kind, int Number);
}
