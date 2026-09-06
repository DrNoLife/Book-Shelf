using CsvHelper.Configuration.Attributes;

namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// Serialization shape of a StoryGraph export row.
/// </summary>
// A dirty boundary model, so: a class, permissive nullability, strings where the
// file has strings, and only the columns the shelf reads out of the 23 StoryGraph
// writes. Mapped to ImportedBook and never travelling further inward
// (CLAUDE.md — Types: records vs classes).
//
// Every column is [Optional]: StoryGraph has changed its export header before,
// and a missing column it never used must not fail the whole import.
public sealed class StoryGraphCsvRow
{
    [Name("Title")]
    [Optional]
    public string? Title { get; set; }

    [Name("Authors")]
    [Optional]
    public string? Authors { get; set; }

    /// <summary>
    /// Everyone else credited, as "Name (Role), Name (Role)" — translators,
    /// editors, and the illustrator the format tie-break needs.
    /// </summary>
    [Name("Contributors")]
    [Optional]
    public string? Contributors { get; set; }

    [Name("ISBN/UID")]
    [Optional]
    public string? Isbn { get; set; }

    /// <summary>Binding, not medium: "digital", "paperback", "hardcover", "audio".</summary>
    [Name("Format")]
    [Optional]
    public string? Format { get; set; }

    /// <summary>"read", "currently-reading", "to-read", "did-not-finish".</summary>
    [Name("Read Status")]
    [Optional]
    public string? ReadStatus { get; set; }

    [Name("Last Date Read")]
    [Optional]
    public string? LastDateRead { get; set; }

    /// <summary>Possibly several dates; the latest is the one that matters.</summary>
    [Name("Dates Read")]
    [Optional]
    public string? DatesRead { get; set; }

    /// <summary>"Yes" or "No" — and in a real export, very often "No" throughout.</summary>
    [Name("Owned?")]
    [Optional]
    public string? Owned { get; set; }
}
