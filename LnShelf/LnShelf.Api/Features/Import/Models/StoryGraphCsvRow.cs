using CsvHelper;

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
[CsvRecord]
public sealed partial class StoryGraphCsvRow
{
    [CsvColumn("Title", Optional = true)]
    public string? Title { get; set; }

    [CsvColumn("Authors", Optional = true)]
    public string? Authors { get; set; }

    /// <summary>
    /// Everyone else credited, as "Name (Role), Name (Role)" — translators,
    /// editors, and the illustrator the format tie-break needs.
    /// </summary>
    [CsvColumn("Contributors", Optional = true)]
    public string? Contributors { get; set; }

    [CsvColumn("ISBN/UID", Optional = true)]
    public string? Isbn { get; set; }

    /// <summary>Binding, not medium: "digital", "paperback", "hardcover", "audio".</summary>
    [CsvColumn("Format", Optional = true)]
    public string? Format { get; set; }

    /// <summary>"read", "currently-reading", "to-read", "did-not-finish".</summary>
    [CsvColumn("Read Status", Optional = true)]
    public string? ReadStatus { get; set; }

    [CsvColumn("Last Date Read", Optional = true)]
    public string? LastDateRead { get; set; }

    /// <summary>Possibly several dates; the latest is the one that matters.</summary>
    [CsvColumn("Dates Read", Optional = true)]
    public string? DatesRead { get; set; }

    /// <summary>"Yes" or "No" — and in a real export, very often "No" throughout.</summary>
    [CsvColumn("Owned?", Optional = true)]
    public string? Owned { get; set; }
}
