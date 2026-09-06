namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// A Goodreads export, read through the production reader.
/// </summary>
// Flagged rather than hidden: this file is SYNTHETIC. testing.md #4 asks for
// captured real payloads, and there is no real Goodreads export in this repo to
// capture — so it is built row by row from Goodreads' published export header
// and the quirks documented against it: Excel-armoured ISBNs (`="978…"`, and
// `=""` for blank), the three built-in Exclusive Shelf values plus a custom one,
// Owned Copies as an integer, and the "(light novel)" edition marker Goodreads
// writes into the title. Replace it with a real export the day one exists.
internal static class GoodreadsExportFixture
{
    public const string FileName = "goodreads-export.csv";

    /// <summary>Books in the fixture.</summary>
    public const int ExpectedBookCount = 9;

    /// <summary>Distinct series those books collapse into.</summary>
    public const int ExpectedGroupCount = 6;

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "Features", "Import", "Fixtures", FileName);

    /// <summary>The file itself, for tests that feed the importer rather than the reader.</summary>
    public static Stream OpenRead() => File.OpenRead(FilePath);

    /// <summary>The header row, as the source resolver sniffs it.</summary>
    public static string HeaderLine() => File.ReadLines(FilePath).First();

    public static async Task<IReadOnlyList<ImportedBook>> ReadAsync()
    {
        await using var stream = OpenRead();

        var reader = new GoodreadsCsvReader(NullLogger<GoodreadsCsvReader>.Instance);
        return await reader.ReadAsync(stream, CancellationToken.None);
    }

    /// <summary>One book by the title the file wrote, for a per-rule assertion.</summary>
    public static async Task<ImportedBook> ReadBookAsync(string rawTitle)
    {
        var books = await ReadAsync();
        return books.Single(book => book.RawTitle == rawTitle);
    }
}
