using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Import.Mappers;

/// <summary>
/// Dirty CSV row → clean book. The dirty→clean edge for Goodreads
/// (CLAUDE.md — Mapping).
/// </summary>
public static partial class GoodreadsBookMapper
{
    public static ImportedBook ToBook(GoodreadsCsvRow row, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rawTitle = row.Title?.Trim() ?? string.Empty;
        var parsedTitle = BookTitleParser.Parse(StripEditionMarker(rawTitle));

        return new ImportedBook
        {
            LineNumber = lineNumber,
            RawTitle = rawTitle,
            SeriesTitle = parsedTitle.SeriesTitle,
            SeriesKey = TitleNormalizer.Normalize(parsedTitle.SeriesTitle),
            VolumeNumber = parsedTitle.VolumeNumber,
            Subtitle = parsedTitle.Subtitle,
            Author = row.Author.NullIfBlank(),
            Illustrator = ToIllustrator(row.AdditionalAuthors),

            // ISBN-13 first: it is the key every enrichment source is looked up by,
            // and an ISBN-10 has to be converted before it is one.
            Isbn = StripExcelArmour(row.Isbn13) ?? StripExcelArmour(row.Isbn),

            ReadStatus = ToReadStatus(row.ExclusiveShelf),
            OwnedInExport = ToOwnedInExport(row.OwnedCopies),
            OwnedFormat = ToOwnedFormat(row.Binding),
            ReadAt = ExportDateParser.LatestDate(row.DateRead),
        };
    }

    /// <summary>
    /// Goodreads' three built-in shelves. Everything else — <c>to-read</c> and any
    /// custom shelf the user made — is a book they have not read.
    /// </summary>
    // A custom shelf is a category, not a progress state: "owned", "isekai" and
    // "abandoned" all sit in this column, and none of them says the book was read.
    private static ReadStatus ToReadStatus(string? exclusiveShelf) =>
        exclusiveShelf?.Trim().ToLowerInvariant() switch
        {
            "read" => ReadStatus.Read,
            "currently-reading" => ReadStatus.Reading,
            _ => ReadStatus.Unread,
        };

    /// <summary>
    /// Only what the column says. Read-implies-owned is applied later, by
    /// <see cref="Services.ImportOwnershipRule"/>, so that the review queue can
    /// still switch it off.
    /// </summary>
    // A count rather than StoryGraph's Yes/No, so any positive number is "owned" —
    // two copies of volume 1 is still one volume on the shelf.
    private static bool ToOwnedInExport(string? ownedCopies) =>
        int.TryParse(ownedCopies?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var copies)
        && copies > 0;

    /// <summary>
    /// Goodreads' binding vocabulary, which is a publisher's word for the object.
    /// </summary>
    // Matched by substring, not equality: "Mass Market Paperback" and "Kindle
    // Edition" are both real values, and an exact switch would send both to the
    // default.
    private static OwnedFormat? ToOwnedFormat(string? binding)
    {
        var value = binding?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(value))
        {
            return Shared.Shelf.OwnedFormat.Physical;
        }

        // An audiobook is a file, not a shelf object — the same call StoryGraph's
        // mapper makes.
        if (value.Contains("kindle", StringComparison.Ordinal)
            || value.Contains("ebook", StringComparison.Ordinal)
            || value.Contains("e-book", StringComparison.Ordinal)
            || value.Contains("audio", StringComparison.Ordinal)
            || value.Contains("audible", StringComparison.Ordinal))
        {
            return Shared.Shelf.OwnedFormat.Digital;
        }

        // Unknown or blank: a book export defaults to a book on a shelf.
        return Shared.Shelf.OwnedFormat.Physical;
    }

    /// <summary>
    /// The first additional author, taken as the illustrator.
    /// </summary>
    // Goodreads does not label the role, so this is the one guess in the mapper.
    // It is a safe one for what this shelf tracks: on a light novel or a manga the
    // second credit is the artist, and the name is only ever used to break a
    // format tie the matcher would otherwise send to review anyway.
    private static string? ToIllustrator(string? additionalAuthors) =>
        additionalAuthors
            .NullIfBlank()
            ?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            .NullIfBlank();

    /// <summary>
    /// Unwraps the <c>="9780316363983"</c> Goodreads writes so Excel does not eat
    /// the leading digits; <c>=""</c> is its empty.
    /// </summary>
    // Stripped here rather than loosened in Isbn.Normalize: the domain type is
    // strict on purpose, and this is one source's spreadsheet quirk, not a shape
    // an ISBN is allowed to have.
    private static string? StripExcelArmour(string? value)
    {
        if (value.NullIfBlank() is not { } armoured)
        {
            return null;
        }

        var match = ExcelArmourPattern().Match(armoured);
        return match.Success ? match.Groups["value"].Value.NullIfBlank() : armoured;
    }

    /// <summary>
    /// Removes the edition marker Goodreads appends to a volume: "…, Vol. 12
    /// (light novel): Subtitle".
    /// </summary>
    // Goodreads titles carry the edition in the title itself, which BookTitleParser
    // has no reason to know about — its patterns expect the volume number to be
    // followed by the subtitle or by nothing. Removed at this source's own edge so
    // the shared parser stays the shared parser.
    private static string StripEditionMarker(string title) =>
        EditionMarkerPattern().Replace(title, string.Empty);

    [GeneratedRegex(@"^=""(?<value>[^""]*)""$", RegexOptions.CultureInvariant)]
    private static partial Regex ExcelArmourPattern();

    [GeneratedRegex(
        @"\s*\((?:light novel|novel|manga|comic|paperback|hardcover)\)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EditionMarkerPattern();
}
