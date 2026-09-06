using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Import.Mappers;

/// <summary>
/// Dirty CSV row → clean book. The dirty→clean edge for StoryGraph
/// (CLAUDE.md — Mapping).
/// </summary>
public static partial class StoryGraphBookMapper
{
    public static ImportedBook ToBook(StoryGraphCsvRow row, int lineNumber)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rawTitle = row.Title?.Trim() ?? string.Empty;
        var parsedTitle = BookTitleParser.Parse(rawTitle);
        var readStatus = ToReadStatus(row.ReadStatus);

        return new ImportedBook
        {
            LineNumber = lineNumber,
            RawTitle = rawTitle,
            SeriesTitle = parsedTitle.SeriesTitle,
            SeriesKey = TitleNormalizer.Normalize(parsedTitle.SeriesTitle),
            VolumeNumber = parsedTitle.VolumeNumber,
            Subtitle = ToSubtitle(parsedTitle.Subtitle),
            Author = row.Authors.NullIfBlank(),
            Illustrator = ToIllustrator(row.Contributors),
            Isbn = row.Isbn.NullIfBlank(),
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
    /// The parsed subtitle, unless it is in Japanese script.
    /// </summary>
    // The parser reports what the title said, which for "オーバーロード1 不死者の王" is a
    // Japanese subtitle. This decides what the shelf will *show*, and the shelf entry is
    // the English one: a Japanese subtitle under an English volume is the same defect
    // DesiredVolumeStateMerger.ChooseIsbn exists to avoid. Dropped here, at the one edge
    // that turns the export into the shelf's own terms, so nothing downstream has to
    // re-ask the question.
    private static string? ToSubtitle(string? subtitle) =>
        subtitle is not null && JapaneseScriptPattern().IsMatch(subtitle) ? null : subtitle;

    /// <summary>
    /// The first contributor StoryGraph credits as the illustrator.
    /// </summary>
    // StoryGraph writes the column as "Name (Role), Name (Role)" and its role
    // vocabulary is freeform, so this matches the two spellings the real export
    // uses. A contributor with no role at all is somebody's editor as often as the
    // artist, and guessing there would feed the matcher a name that is not the one
    // AniList credits.
    private static string? ToIllustrator(string? contributors)
    {
        if (contributors.NullIfBlank() is not { } credited)
        {
            return null;
        }

        foreach (var contributor in credited.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (IllustratorRolePattern().Match(contributor) is { Success: true } match)
            {
                return match.Groups["name"].Value.NullIfBlank();
            }
        }

        return null;
    }

    /// <summary>
    /// "Last Date Read" first, then the latest of "Dates Read" — a re-read leaves
    /// several dates in that column and the newest is the one the shelf shows.
    /// </summary>
    private static DateOnly? ToReadAt(StoryGraphCsvRow row) =>
        ExportDateParser.LatestDate(row.LastDateRead) ?? ExportDateParser.LatestDate(row.DatesRead);

    // The same three blocks BookTitleParser matches a Japanese title on.
    [GeneratedRegex(
        @"[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}]",
        RegexOptions.CultureInvariant)]
    private static partial Regex JapaneseScriptPattern();

    [GeneratedRegex(
        @"^(?<name>.+?)\s*\((?:Illustrator|Illustrations|Artist|Art)\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IllustratorRolePattern();
}
