namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The review file's row shape. A spreadsheet is going to open this, and a person is
/// going to delete the rows they do not believe.
/// </summary>
// Dirty boundary model: a class with settable members, because it maps to an external
// format (CLAUDE.md — Types). Column names are the header text, chosen to be readable
// rather than to match the C# names.
public sealed class IsbnReviewCsvRow
{
    public string SeriesId { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int Volume { get; set; }
    public string? Isbn { get; set; }

    /// <summary>What Open Library calls the edition — the field to sanity-check against.</summary>
    public string? OpenLibraryTitle { get; set; }

    public string? Publisher { get; set; }

    /// <summary>The outcome, or the reason there is no ISBN.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;
}
