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
        var title = Normalize(rawTitle);

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

        // "Overlord, Vol. 10.5" and "Overlord, Vols. 1-2": a bonus volume or an omnibus.
        // The volume number is a whole number, so neither is guessed; the series name is
        // still right, and a book without a number beside numbered ones goes to review
        // (UnplaceableBookFinder) instead of becoming a series named after the raw title.
        if (UnnumberableVolumePattern().Match(title) is { Success: true } unnumberableMatch)
        {
            return new ParsedBookTitle { SeriesTitle = unnumberableMatch.Groups["series"].Value.Trim() };
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

    /// <summary>
    /// Whether the title names a bonus or omnibus volume ("Vol. 10.5", "Vols. 1-2"): one
    /// that belongs to a series but to no whole volume of it.
    /// </summary>
    // Parse gives such a title the clean series name and no number, and a missing number
    // otherwise means "the series in one volume". Read from the raw title, so a staged
    // row can be checked without a stored flag. Every parenthetical goes first, not only
    // the format tags Parse knows: the raw title is kept verbatim, and Goodreads' binding
    // markers ("(paperback)") are only stripped by its mapper before parsing. A
    // parenthetical never supplies a volume marker, so dropping it cannot invent one.
    public static bool HasUnnumberableVolume(string? rawTitle)
    {
        var title = AnyParentheticalPattern().Replace(Normalize(rawTitle), string.Empty).Trim();

        return UnnumberableVolumePattern().IsMatch(title);
    }

    /// <summary>
    /// NFKC folds the full-width digits Japanese editions are often written with
    /// ("転生したらスライムだった件１３") into ASCII, so they parse as the volume they are.
    /// </summary>
    private static string Normalize(string? rawTitle) =>
        StripTrailingBracket(rawTitle?.Normalize(NormalizationForm.FormKC).Trim() ?? string.Empty).Trim();

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

    // Every number is [0-9], never \d: in .NET \d is any Unicode digit, and one that NFKC
    // does not fold (Arabic-Indic, Devanagari) would reach int.Parse and fail the whole
    // upload. Such a title stays whole instead.
    [GeneratedRegex(
        @"^(?<series>.+?):\s*Part\s+(?<part>[0-9]{1,2})\s+Vol(?:ume|\.)?\s*(?<volume>[0-9]{1,3})\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PartAndVolumePattern();

    // "Light novel", "novel" and "manga" only: other parentheses in a title are its own
    // words, like the "(Finish)" in an 86 subtitle.
    [GeneratedRegex(@"\s*\((?:light\s+novel|novel|manga)\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex FormatTagPattern();

    // A decimal ("Vol. 10.5") or a range ("Vols. 1-2", "Volumes 1–3"). A decimal may take
    // either subtitle separator VolumeMarkerPattern accepts; a range only a colon, so
    // "Vol. 3 - 100 Days" is still volume 3 with a subtitle, not volumes 3 to 100.
    [GeneratedRegex(
        @"^(?<series>.+?)(?:,\s*|\s+)Vol(?:ume)?s?\.?\s*[0-9]{1,3}(?:\.[0-9]+(?:(?:\s*:|\s+[-–—])\s*.+)?|\s*[-–]\s*[0-9]{1,3}(?:\s*:\s*.+)?)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex UnnumberableVolumePattern();

    [GeneratedRegex(@"\s*\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex AnyParentheticalPattern();

    // Vol after a comma or a space, never inside a word: "Revolution 3" is not volume 3.
    // The subtitle follows a colon or a spaced dash ("Vol. 3 - The Bloody Valkyrie"); the
    // spaces matter, because "86—EIGHTY-SIX" has dashes in the series' own name.
    [GeneratedRegex(
        @"^(?<series>.+?)(?:,\s*|\s+)Vol(?:ume|\.)?\s*(?<volume>[0-9]{1,3})(?:(?:\s*:|\s+[-–—])\s*(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex VolumeMarkerPattern();

    [GeneratedRegex(
        @"^(?<series>.+?)\s+(?<volume>[0-9]{1,3})(?:\s*:\s*(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberPattern();

    [GeneratedRegex(
        @"^(?<series>.*[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}])\s*(?<volume>[0-9]{1,3})(?:\s+(?<subtitle>.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CjkTrailingVolumePattern();
}
