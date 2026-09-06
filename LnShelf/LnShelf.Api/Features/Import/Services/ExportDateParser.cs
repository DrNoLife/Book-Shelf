using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// The read dates out of an export's date field.
/// </summary>
// StoryGraph and Goodreads both write yyyy/mm/dd, and both accept dashes, so this
// is one rule rather than one per source. It lives beside BookTitleParser for the
// same reason: it is about the shape of book-export data, not about which product
// produced it.
public static partial class ExportDateParser
{
    /// <summary>Exports write dates as 2021/11/23; some use dashes.</summary>
    private static readonly string[] DateFormats = ["yyyy/MM/dd", "yyyy-MM-dd"];

    /// <summary>
    /// The latest date anywhere in the field, ignoring whatever separates them.
    /// </summary>
    // One field, three shapes: a single date, a comma list of re-reads, and a
    // "2026/07/21-2026/07/26" range for a read that spanned days — which the real
    // export contains, and which splitting on a comma parsed to nothing at all.
    // Lifting the dates out by pattern covers all three without the parser having to
    // know which separator the source reached for, and a fourth shape costs nothing.
    public static DateOnly? LatestDate(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        DateOnly? latest = null;

        foreach (var match in DatePattern().EnumerateMatches(field))
        {
            // The pattern accepts a mixed separator ("2026/07-21") that neither format
            // parses, so a match is a candidate rather than a guarantee.
            if (DateOnly.TryParseExact(
                    field.AsSpan(match.Index, match.Length),
                    DateFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed)
                && (latest is null || parsed > latest))
            {
                latest = parsed;
            }
        }

        return latest;
    }

    [GeneratedRegex(@"\d{4}[/-]\d{2}[/-]\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}
