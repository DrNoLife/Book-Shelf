using System.Globalization;

namespace LnShelf.Api.Features.Import.Mappers;

/// <summary>
/// Dirty CSV row → clean book. The dirty→clean edge for StoryGraph
/// (CLAUDE.md — Mapping).
/// </summary>
public static class StoryGraphBookMapper
{
    /// <summary>StoryGraph writes dates as 2021/11/23, and several separated by commas.</summary>
    private static readonly string[] DateFormats = ["yyyy/MM/dd", "yyyy-MM-dd"];

    public static StoryGraphBook ToBook(StoryGraphCsvRow row, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rawTitle = row.Title?.Trim() ?? string.Empty;
        var parsedTitle = BookTitleParser.Parse(rawTitle);
        var readStatus = ToReadStatus(row.ReadStatus);

        return new StoryGraphBook
        {
            LineNumber = lineNumber,
            RawTitle = rawTitle,
            SeriesTitle = parsedTitle.SeriesTitle,
            SeriesKey = TitleNormalizer.Normalize(parsedTitle.SeriesTitle),
            VolumeNumber = parsedTitle.VolumeNumber,
            Author = NullIfBlank(row.Authors),
            Isbn = NullIfBlank(row.Isbn),
            ReadStatus = readStatus,
            OwnedInExport = ToOwnedInExport(row.Owned),
            OwnedFormat = ToOwnedFormat(row.Format),
            ReadAt = ToReadAt(row),
        };
    }

    private static ReadStatus ToReadStatus(string? storyGraphStatus) =>
        storyGraphStatus?.Trim().ToLowerInvariant() switch
        {
            "read" => ReadStatus.Read,
            "currently-reading" => ReadStatus.Reading,

            // "did-not-finish" is abandoned, not in progress. The shelf has no
            // per-volume "dropped" — that lives on the series as a tracking status,
            // and it is the user's call, not the importer's.
            "did-not-finish" or "to-read" or _ => ReadStatus.Unread,
        };

    /// <summary>
    /// Only what the column says. Read-implies-owned is applied later, by
    /// <see cref="Services.ImportOwnershipRule"/>, so that the review queue can
    /// still switch it off.
    /// </summary>
    private static bool ToOwnedInExport(string? storyGraphOwned) =>
        string.Equals(storyGraphOwned?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);

    private static OwnedFormat? ToOwnedFormat(string? storyGraphFormat) =>
        storyGraphFormat?.Trim().ToLowerInvariant() switch
        {
            "digital" or "ebook" or "e-book" => Shared.Shelf.OwnedFormat.Digital,

            // An audiobook is a file, not a shelf object.
            "audio" or "audiobook" => Shared.Shelf.OwnedFormat.Digital,

            "paperback" or "hardcover" or "hardback" or "physical" => Shared.Shelf.OwnedFormat.Physical,

            // Unknown or blank: a book export defaults to a book on a shelf.
            _ => Shared.Shelf.OwnedFormat.Physical,
        };

    /// <summary>
    /// "Last Date Read" first, then the latest of "Dates Read" — a re-read leaves
    /// several dates in that column and the newest is the one the shelf shows.
    /// </summary>
    private static DateOnly? ToReadAt(StoryGraphCsvRow row) =>
        ParseDate(row.LastDateRead) ?? LatestDate(row.DatesRead);

    private static DateOnly? LatestDate(string? datesRead)
    {
        if (string.IsNullOrWhiteSpace(datesRead))
        {
            return null;
        }

        DateOnly? latest = null;
        foreach (var candidate in datesRead.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parsed = ParseDate(candidate);
            if (parsed is not null && (latest is null || parsed > latest))
            {
                latest = parsed;
            }
        }

        return latest;
    }

    private static DateOnly? ParseDate(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateOnly.TryParseExact(
            value.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
