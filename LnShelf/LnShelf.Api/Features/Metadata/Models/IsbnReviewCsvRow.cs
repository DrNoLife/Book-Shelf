using CsvHelper;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The review file's row shape. A spreadsheet is going to open this, and a person is
/// going to delete the rows they do not believe.
/// </summary>
// Dirty boundary model: a class with settable members, because it maps to an external
// format (CLAUDE.md — Types). Column names are the header text, chosen to be readable
// rather than to match the C# names.
[CsvRecord]
public sealed partial class IsbnReviewCsvRow
{
    public string SeriesId { get; set; } = string.Empty;
    [CsvColumn(Optional = true)]
    public string Series { get; set; } = string.Empty;
    public decimal Volume { get; set; }
    public string? Isbn { get; set; }

    /// <summary>What Open Library calls the edition — the field to sanity-check against.</summary>
    [CsvColumn(Optional = true)]
    public string? OpenLibraryTitle { get; set; }

    [CsvColumn(Optional = true)]
    public string? Publisher { get; set; }

    /// <summary>The outcome, or the reason there is no ISBN.</summary>
    [CsvColumn(Optional = true)]
    public string Outcome { get; set; } = string.Empty;

    [CsvColumn(Optional = true)]
    public string Note { get; set; } = string.Empty;
}
