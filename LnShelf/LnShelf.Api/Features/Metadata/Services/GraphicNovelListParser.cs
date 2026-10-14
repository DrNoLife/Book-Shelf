using System.Text.RegularExpressions;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// A Wikipedia page's wikitext into its <c>{{Graphic novel list}}</c> tables: per row, the
/// volume number, the English edition's ISBNs and the chapters. Pure.
/// </summary>
// Wikipedia has no structured API for these lists, so the template is the contract. Its
// parameter names vary by page and are read loosely — VolumeNumber, LicensedISBN, and every
// parameter starting ChapterList (ChapterList, ChapterListCol1, ChapterListCol2). The original
// edition's ISBN and both dates are left alone: verification is by the English edition, and
// the dates are another seam's business.
public static partial class GraphicNovelListParser
{
    private const string RowTemplate = "Graphic novel list";
    private const string HeaderTemplate = "Graphic novel list/header";

    public static IReadOnlyList<GraphicNovelListTable> Parse(string wikitext)
    {
        ArgumentNullException.ThrowIfNull(wikitext);

        var headerStarts = WikitextTemplates.Find(wikitext, HeaderTemplate).Select(header => header.Start).ToList();

        // A row belongs to the table of the last header above it; rows above every header (a
        // page that never opens one) form a table of their own.
        return
        [
            .. WikitextTemplates.Find(wikitext, RowTemplate)
                .GroupBy(row => headerStarts.Count(start => start < row.Start))
                .Select(rows => new GraphicNovelListTable([.. rows.Select(row => ToEntry(row.Body))])),
        ];
    }

    private static GraphicNovelListEntry ToEntry(string body)
    {
        var parameters = WikitextTemplates.Parameters(body);

        var chapterList = string.Join(
            "\n",
            parameters
                .Where(parameter => parameter.Key.StartsWith("ChapterList", StringComparison.OrdinalIgnoreCase))
                .Select(parameter => parameter.Value));

        return new GraphicNovelListEntry(
            VolumeNumber(parameters.GetValueOrDefault("VolumeNumber")),
            LicensedIsbns(parameters.GetValueOrDefault("LicensedISBN")),
            ChapterListReader.Read(chapterList));
    }

    private static decimal? VolumeNumber(string? value) =>
        value is not null
        && decimal.TryParse(
            WikitextTemplates.WithoutReferences(value),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var number)
            ? number
            : null;

    // Every ISBN-shaped run in the value, because a row may give the print and the digital
    // edition side by side. Read after the references go, so a citation's ISBN never counts.
    private static IReadOnlyList<string> LicensedIsbns(string? value) =>
        value is null
            ? []
            :
            [
                .. IsbnShaped().Matches(WikitextTemplates.WithoutReferences(value))
                    .Select(match => Isbn.ToIsbn13(match.Value))
                    .OfType<string>()
                    .Distinct(),
            ];

    // Thirteen or ten characters of ISBN alphabet, hyphens and spaces allowed between them.
    [GeneratedRegex(@"(?<![\dXx])(?:97[89][\s-]?)?(?:\d[\s-]?){9}[\dXx](?![\dXx])")]
    private static partial Regex IsbnShaped();
}
