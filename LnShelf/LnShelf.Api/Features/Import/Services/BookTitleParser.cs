using System.Globalization;
using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Splits a StoryGraph book title into series and volume.
/// </summary>
// Every pattern here came out of one real 76-row export, and each has a
// regression test. The order matters: the most specific shape is tried first, and
// the fall-through is "this is a standalone book", never a guess.
public static partial class BookTitleParser
{
    public static ParsedBookTitle Parse(string? rawTitle)
    {
        var title = StripTrailingBracket(rawTitle?.Trim() ?? string.Empty).Trim();

        if (title.Length == 0)
        {
            return new ParsedBookTitle { SeriesTitle = string.Empty };
        }

        // "Ascendance of a Bookworm: Part 2 Volume 3". The part belongs to the
        // series' identity, so it is folded back into the title.
        if (PartAndVolumePattern().Match(title) is { Success: true } partMatch)
        {
            var part = int.Parse(partMatch.Groups["part"].ValueSpan, CultureInfo.InvariantCulture);

            return new ParsedBookTitle
            {
                SeriesTitle = $"{partMatch.Groups["series"].Value.Trim()}: Part {part}",
                Part = part,
                VolumeNumber = int.Parse(partMatch.Groups["volume"].ValueSpan, CultureInfo.InvariantCulture),
            };
        }

        // "Overlord, Vol. 3: The Bloody Valkyrie" and its "Volume 15" spelling —
        // both appear in the same export, for the same series.
        if (CommaVolumePattern().Match(title) is { Success: true } commaMatch)
        {
            return new ParsedBookTitle
            {
                SeriesTitle = commaMatch.Groups["series"].Value.Trim(),
                VolumeNumber = int.Parse(commaMatch.Groups["volume"].ValueSpan, CultureInfo.InvariantCulture),
                Subtitle = NullIfBlank(commaMatch.Groups["subtitle"].Value),
            };
        }

        // "オーバーロード11 山小人の工匠" and "転生したらスライムだった件1": Japanese
        // editions run the volume number straight onto the title. Guarded on the
        // title actually containing Japanese, so an English title that happens to
        // end in a digit is never carved up.
        if (CjkTrailingVolumePattern().Match(title) is { Success: true } cjkMatch)
        {
            return new ParsedBookTitle
            {
                SeriesTitle = cjkMatch.Groups["series"].Value.Trim(),
                VolumeNumber = int.Parse(cjkMatch.Groups["volume"].ValueSpan, CultureInfo.InvariantCulture),
                Subtitle = NullIfBlank(cjkMatch.Groups["subtitle"].Value),
            };
        }

        return new ParsedBookTitle { SeriesTitle = title };
    }

    /// <summary>
    /// "転生したらスライムだった件 13 [Tensei shitara Slime Datta Ken 13]" — StoryGraph
    /// appends a romanisation in brackets, and it would otherwise be matched as
    /// part of the title.
    /// </summary>
    private static string StripTrailingBracket(string title) =>
        TrailingBracketPattern().Replace(title, string.Empty);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\s*\[[^\]]*\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingBracketPattern();

    [GeneratedRegex(
        @"^(?<series>.+?):\s*Part\s+(?<part>\d{1,2})\s+Vol(?:ume|\.)?\s*(?<volume>\d{1,3})\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PartAndVolumePattern();

    [GeneratedRegex(
        @"^(?<series>.+?),\s*Vol(?:ume|\.)?\s*(?<volume>\d{1,3})(?:\s*:\s*(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex CommaVolumePattern();

    [GeneratedRegex(
        @"^(?<series>.*[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}])\s*(?<volume>\d{1,3})(?:\s+(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CjkTrailingVolumePattern();
}
