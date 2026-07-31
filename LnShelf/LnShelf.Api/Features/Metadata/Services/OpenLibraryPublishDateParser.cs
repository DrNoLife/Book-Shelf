namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Open Library's <c>publish_date</c> is free text. This reads the forms that carry
/// a whole day and refuses the ones that do not.
/// </summary>
// All three shapes below are real, taken from this shelf's own ISBNs in one sample:
// "2017-01-31" (Overlord 3), "Apr 24, 2018" (Slime 2), "2022" (Slime 14). Only the
// first two are release dates. A year, or a year and a month, is a fact about a
// volume that is not a date, and the shelf already has an honest place to say "out,
// date unknown" — Volume.IsReleased. Rounding "2022" up to 1 January would put a
// wrong date in the ghost tile's caption and in a future Upcoming view, which is
// worse than no date at all.
public static class OpenLibraryPublishDateParser
{
    private static readonly string[] FullDateFormats =
    [
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "MMM d, yyyy",
        "MMMM d, yyyy",
        "d MMM yyyy",
        "d MMMM yyyy",
        "MMM d yyyy",
        "MMMM d yyyy",
    ];

    /// <returns>The date, or null when the text names no single day.</returns>
    public static DateOnly? Parse(string? publishDate)
    {
        if (string.IsNullOrWhiteSpace(publishDate))
        {
            return null;
        }

        var trimmed = publishDate.Trim();

        // Invariant culture on purpose: the field is English-language metadata, and
        // parsing "Apr 24, 2018" under a Danish culture would fail on the month name.
        return DateOnly.TryParseExact(
            trimmed, FullDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
