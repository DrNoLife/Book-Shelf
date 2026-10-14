using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Reads the chapter numbers out of one Graphic novel list row's chapter list, in whichever of
/// the formats Wikipedia's editors use for it. Pure.
/// </summary>
// The formats, as found on the canary pages (2026-10-14):
//   {{Numbered list|start=5|title|title}}  chapters counted from start (Chainsaw Man, Frieren)
//   * 048. "Title" / * 049–050. "Title"     a number leads each line (Berserk)
//   *Mission: 1–5 / *Short Mission: 1       the first labelled range only (Spy × Family); the
//                                           lines after it number extras on their own scale
//   * "Title" (no numbers), or nothing      unmappable: null, never a guess
// Tried in that order: a row that numbers its lines is never also read as a labelled range.
public static partial class ChapterListReader
{
    private static readonly string[] ListTemplateNames = ["Numbered list", "Ordered list"];

    /// <returns>The row's chapters in ascending order, or null when it does not number them.</returns>
    public static IReadOnlyList<decimal>? Read(string chapterList)
    {
        ArgumentNullException.ThrowIfNull(chapterList);

        var text = WikitextTemplates.WithoutReferences(chapterList);

        List<decimal> chapters = [.. FromListTemplates(text)];
        if (chapters.Count == 0)
        {
            chapters = [.. FromLeadingNumbers(text)];
        }

        if (chapters.Count == 0)
        {
            chapters = [.. FromFirstLabelledRange(text)];
        }

        return chapters.Count == 0 ? null : [.. chapters.Distinct().Order()];
    }

    /// <summary>
    /// Every chapter from "start" to "start + items − 1", where a list starts at 1 unless it says otherwise.
    /// </summary>
    private static IEnumerable<decimal> FromListTemplates(string text)
    {
        foreach (var template in ListTemplateNames.SelectMany(name => WikitextTemplates.Find(text, name)))
        {
            var parts = WikitextTemplates.Split(template.Body).Skip(1).ToList();
            var start = parts.Select(part => ListStart().Match(part)).FirstOrDefault(match => match.Success) is { } startMatch
                ? int.Parse(startMatch.Groups["start"].Value, CultureInfo.InvariantCulture)
                : 1;

            // Named parameters (start=, list_style_type=) are settings, not chapters.
            var items = parts.Count(part => !string.IsNullOrWhiteSpace(part) && !NamedParameter().IsMatch(part));

            for (var offset = 0; offset < items; offset++)
            {
                yield return start + offset;
            }
        }
    }

    private static IEnumerable<decimal> FromLeadingNumbers(string text) =>
        LeadingNumber().Matches(text).SelectMany(Spanned);

    private static IEnumerable<decimal> FromFirstLabelledRange(string text) =>
        LabelledRange().Match(text) is { Success: true } match ? Spanned(match) : [];

    /// <summary>The first number, and every whole chapter up to the second when the line gives a range.</summary>
    private static IEnumerable<decimal> Spanned(Match match)
    {
        var first = decimal.Parse(match.Groups["first"].Value, CultureInfo.InvariantCulture);

        if (!match.Groups["last"].Success)
        {
            return [first];
        }

        var last = decimal.Parse(match.Groups["last"].Value, CultureInfo.InvariantCulture);

        // A backwards range is a typo, and its first number is the only part worth believing.
        if (last <= first)
        {
            return [first];
        }

        var wholes = Enumerable
            .Range((int)decimal.Ceiling(first), (int)decimal.Floor(last) - (int)decimal.Ceiling(first) + 1)
            .Select(chapter => (decimal)chapter);

        return [first, .. wholes, last];
    }

    private const string Number = @"\d+(?:\.\d+)?";

    // En dash, em dash, hyphen, and the two ways wikitext spells an en dash.
    private const string Dash = @"\s*(?:–|—|-|&ndash;|\{\{ndash\}\})\s*";

    [GeneratedRegex(@"^\s*start\s*=\s*(?<start>\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ListStart();

    [GeneratedRegex(@"^\s*[a-z][a-z0-9_]*\s*=")]
    private static partial Regex NamedParameter();

    [GeneratedRegex(@"^\*+\s*(?:'{2,3})?(?<first>" + Number + ")(?:" + Dash + "(?<last>" + Number + @"))?\s*[.:]", RegexOptions.Multiline)]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"^\*+\s*[^\d:\n*|{}\[\]]+?\s*:\s*(?<first>" + Number + ")(?:" + Dash + "(?<last>" + Number + "))?", RegexOptions.Multiline)]
    private static partial Regex LabelledRange();
}
