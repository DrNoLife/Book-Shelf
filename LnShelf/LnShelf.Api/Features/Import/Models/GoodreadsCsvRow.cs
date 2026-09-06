using CsvHelper.Configuration.Attributes;

namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// Serialization shape of a Goodreads export row.
/// </summary>
// A dirty boundary model, so: a class, permissive nullability, strings where the
// file has strings, and only the columns the shelf reads out of the 24 Goodreads
// writes. Mapped to ImportedBook and never travelling further inward
// (CLAUDE.md — Types: records vs classes).
//
// Deliberately not read (D3): Date Added, Read Count, My Rating, Average Rating,
// My Review, Private Notes, Bookshelves. Ratings and reviews are Stats territory
// and post-v1; Date Added is when the book was shelved, and mapping it to a read
// date would fabricate history.
//
// Every column is [Optional]: Goodreads has changed its export header before, and
// a missing column it never used must not fail the whole import.
public sealed class GoodreadsCsvRow
{
    [Name("Title")]
    [Optional]
    public string? Title { get; set; }

    [Name("Author")]
    [Optional]
    public string? Author { get; set; }

    /// <summary>
    /// Everyone credited besides the main author, comma-separated and unlabelled —
    /// for a light novel that is usually the illustrator.
    /// </summary>
    [Name("Additional Authors")]
    [Optional]
    public string? AdditionalAuthors { get; set; }

    /// <summary>ISBN-10, wrapped in Excel armour: <c>="0316363987"</c>.</summary>
    [Name("ISBN")]
    [Optional]
    public string? Isbn { get; set; }

    /// <summary>ISBN-13, likewise armoured, and the one the shelf prefers.</summary>
    [Name("ISBN13")]
    [Optional]
    public string? Isbn13 { get; set; }

    /// <summary>"Paperback", "Kindle Edition", "Mass Market Paperback", "Audible Audio".</summary>
    [Name("Binding")]
    [Optional]
    public string? Binding { get; set; }

    /// <summary>One date only — Goodreads exports no per-read history.</summary>
    [Name("Date Read")]
    [Optional]
    public string? DateRead { get; set; }

    /// <summary>"read", "currently-reading", "to-read", or a custom shelf name.</summary>
    [Name("Exclusive Shelf")]
    [Optional]
    public string? ExclusiveShelf { get; set; }

    /// <summary>An integer, not a Yes/No: 0 for a book the user does not own.</summary>
    [Name("Owned Copies")]
    [Optional]
    public string? OwnedCopies { get; set; }
}
