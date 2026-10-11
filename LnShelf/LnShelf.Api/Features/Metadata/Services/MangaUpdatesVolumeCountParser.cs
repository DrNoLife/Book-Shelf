using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Pulls a volume count out of MangaUpdates' free-text status field.
/// </summary>
// There is no structured volume count in the API, so this parses prose. Every
// pattern here is a real status string from a series on the shelf, and each one is
// a test case. The ordering is the whole design: a light-novel count beats a bare
// count beats a web-novel count, because a shelf tracks published books and the
// same series routinely reports several numbers at once —
// "14 WN Volumes + 2 SS Volumes (Complete) \n 23 LN Volumes + 2 SS Volumes (Complete)"
// means 23, not 14.
public static partial class MangaUpdatesVolumeCountParser
{
    /// <returns>Null when the text says nothing worth trusting.</returns>
    public static int? Parse(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return FirstMatch(LightNovelVolumesPattern(), status)
               ?? FirstMatch(PlainVolumesPattern(), status)
               ?? FirstMatch(WebNovelVolumesPattern(), status);
    }

    /// <summary>
    /// The English edition's volume count, from the English publishers' own notes.
    /// </summary>
    /// <returns>Null when no English publisher states a count of books.</returns>
    // Preferred over the status line, which counts the Japanese original: Slime's status says
    // "23 LN Volumes" while Yen On's note says 22, and 22 is what a reader of the English
    // edition can actually buy. The note also keeps bonus volumes out of the count —
    // "9 Volumes + 1 Extra", "11+3 Volumes" — which are tracked from the store catalogue as
    // Vol. 7.5 and the like, not as volumes 10 to 14.
    //
    // The highest count wins when several English publishers state one: a series moves between
    // licensors, and the older one's note stops where it stopped. Audiobook and chapter counts
    // ("14 Audiobooks", "146 Chapters") never match, and neither does a qualified count such as
    // "33 Digital Volumes", which falls back to the status line rather than being guessed at.
    public static int? ParseEnglishEditions(IEnumerable<string?> englishPublisherNotes)
    {
        ArgumentNullException.ThrowIfNull(englishPublisherNotes);

        return englishPublisherNotes
            .Where(notes => !string.IsNullOrWhiteSpace(notes))
            .Select(notes => FirstMatch(EnglishEditionVolumesPattern(), notes!))
            .Max();
    }

    /// <summary>
    /// How many bonus volumes the English edition counts beside its main ones: the 1 in
    /// "9 Volumes + 1 Extra", the 3 in "11+3 Volumes".
    /// </summary>
    /// <returns>Zero when no English publisher states any.</returns>
    // Not a count the shelf creates volumes from — the note never says where a bonus volume
    // sits, and only the store catalogue's "Vol. 7.5" does. It is the signal that there is
    // something to look for: a series whose volumes are all enriched is not asked about
    // again, and without this a bonus volume would never be found on it.
    public static int ParseEnglishEditionExtras(IEnumerable<string?> englishPublisherNotes)
    {
        ArgumentNullException.ThrowIfNull(englishPublisherNotes);

        return englishPublisherNotes
            .Where(notes => !string.IsNullOrWhiteSpace(notes))
            .Select(notes => EnglishEditionExtrasPattern().Match(notes!))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups["extra"].ValueSpan, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// The line of the status the count is read from, so the publication status can be
    /// read from the same line.
    /// </summary>
    /// <returns>Null when no line carries a count.</returns>
    // "14 WN Volumes (Complete)\n23 LN Volumes (Ongoing)" is an ongoing light novel. Read
    // from the whole text, "complete" in the web-novel line flipped it to Completed,
    // while the count came from the LN line (audit 2026-10-09).
    public static string? CountedLine(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return FirstLineMatching(LightNovelVolumesPattern(), lines)
               ?? FirstLineMatching(PlainVolumesPattern(), lines)
               ?? FirstLineMatching(WebNovelVolumesPattern(), lines);
    }

    private static string? FirstLineMatching(Regex pattern, string[] lines) =>
        lines.FirstOrDefault(line => FirstMatch(pattern, line) is not null);

    private static int? FirstMatch(Regex pattern, string status)
    {
        var match = pattern.Match(status);

        return match.Success
               && int.TryParse(match.Groups["count"].ValueSpan, CultureInfo.InvariantCulture, out var count)
               && count > 0
            ? count
            : null;
    }

    /// <summary>"23 LN Volumes (Complete)" — the published-book count.</summary>
    [GeneratedRegex(@"(?<count>\d{1,4})\s*LN\s+Volumes?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LightNovelVolumesPattern();

    /// <summary>
    /// "14 Volumes (Ongoing)" — no qualifier, so it is the series' own count.
    /// </summary>
    // A qualifier between the number and the word is what excludes the counts this
    // must not pick up: "2 SS Volumes" and "2 Gaiden Volumes" simply do not match,
    // because the digits are not adjacent to "Volumes". The leftmost match wins, so
    // "33 Volumes (Complete) … Part 1: 3 Volumes" reads 33 rather than 3.
    [GeneratedRegex(@"(?<count>\d{1,4})\s*Volumes?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PlainVolumesPattern();

    /// <summary>
    /// "9 Volumes + 1 Extra", "9+2 Volume", "13 Volumes - Ongoing" — the main count, with any
    /// bonus volumes after a plus sign left out of it.
    /// </summary>
    // The lookbehind keeps the count a whole token: "6 2-in-1 Volumes" is six omnibuses, and
    // without it the 1 of "2-in-1" would be read as a count of one.
    [GeneratedRegex(
        @"(?<![\w-])(?<count>\d{1,4})\s*(?:\+\s*\d{1,3}\s*)?Volumes?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EnglishEditionVolumesPattern();

    /// <summary>"9 Volumes + 1 Extra", "9+2 Volume" — the number after the plus sign.</summary>
    [GeneratedRegex(
        @"\d{1,4}\s*(?:Volumes?\s*)?\+\s*(?<extra>\d{1,3})",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EnglishEditionExtrasPattern();

    /// <summary>
    /// "14 WN Volumes (Complete)" — last resort. A web-novel count is better than
    /// no count, and the discrepancy row records where it came from.
    /// </summary>
    [GeneratedRegex(@"(?<count>\d{1,4})\s*WN\s+Volumes?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WebNovelVolumesPattern();
}
