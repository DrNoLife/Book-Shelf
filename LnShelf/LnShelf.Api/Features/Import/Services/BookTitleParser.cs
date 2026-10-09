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

        // "Overlord, Vol. 1 (light novel)" and "Mushoku Tensei (Light Novel) Vol. 3": the
        // publisher's format tag is not part of the series' name, and wherever it sits it
        // stops the volume patterns below from seeing the number.
        var hasFormatTag = FormatTagPattern().IsMatch(title);
        title = FormatTagPattern().Replace(title, string.Empty).Trim();

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
        // both appear in the same export, for the same series. The comma is optional:
        // once a format tag is gone, "Mushoku Tensei Vol. 3" is what remains.
        if (VolumeMarkerPattern().Match(title) is { Success: true } markerMatch)
        {
            return FromMatch(markerMatch);
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
                Subtitle = cjkMatch.Groups["subtitle"].Value.NullIfBlank(),
            };
        }

        // "Sword Art Online 1 (light novel)": a bare trailing number, trusted only because
        // the format tag said this is one volume of a series. Without the tag the same
        // shape is "Catch 22", which must stay whole.
        if (hasFormatTag && TrailingNumberPattern().Match(title) is { Success: true } numberMatch)
        {
            return FromMatch(numberMatch);
        }

        return new ParsedBookTitle { SeriesTitle = title };
    }

    private static ParsedBookTitle FromMatch(Match match) => new()
    {
        SeriesTitle = match.Groups["series"].Value.Trim(),
        VolumeNumber = int.Parse(match.Groups["volume"].ValueSpan, CultureInfo.InvariantCulture),
        Subtitle = match.Groups["subtitle"].Value.NullIfBlank(),
    };

    /// <summary>
    /// "転生したらスライムだった件 13 [Tensei shitara Slime Datta Ken 13]" — StoryGraph
    /// appends a romanisation in brackets, and it would otherwise be matched as
    /// part of the title.
    /// </summary>
    private static string StripTrailingBracket(string title) =>
        TrailingBracketPattern().Replace(title, string.Empty);

    [GeneratedRegex(@"\s*\[[^\]]*\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingBracketPattern();

    [GeneratedRegex(
        @"^(?<series>.+?):\s*Part\s+(?<part>\d{1,2})\s+Vol(?:ume|\.)?\s*(?<volume>\d{1,3})\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PartAndVolumePattern();

    // "Light novel", "novel" and "manga" only: other parentheses in a title are its own
    // words, like the "(Finish)" in an 86 subtitle.
    [GeneratedRegex(@"\s*\((?:light\s+novel|novel|manga)\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FormatTagPattern();

    // Vol after a comma or a space, never inside a word: "Revolution 3" is not volume 3.
    [GeneratedRegex(
        @"^(?<series>.+?)(?:,\s*|\s+)Vol(?:ume|\.)?\s*(?<volume>\d{1,3})(?:\s*:\s*(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex VolumeMarkerPattern();

    [GeneratedRegex(
        @"^(?<series>.+?)\s+(?<volume>\d{1,3})(?:\s*:\s*(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberPattern();

    [GeneratedRegex(
        @"^(?<series>.*[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}])\s*(?<volume>\d{1,3})(?:\s+(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CjkTrailingVolumePattern();
}
