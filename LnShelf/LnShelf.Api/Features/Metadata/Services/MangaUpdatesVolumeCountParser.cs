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
    /// "14 WN Volumes (Complete)" — last resort. A web-novel count is better than
    /// no count, and the discrepancy row records where it came from.
    /// </summary>
    [GeneratedRegex(@"(?<count>\d{1,4})\s*WN\s+Volumes?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WebNovelVolumesPattern();
}
