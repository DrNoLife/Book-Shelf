namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// The owner's real StoryGraph export, anonymised, read through the production reader.
/// </summary>
// testing.md #2: every mismatch found during the real import becomes a regression
// case. This is that file, 76 books, with what made it personal taken out before the
// repository went public: reviews and star ratings are blank, and every date is moved
// back by the same 1000 days, so their order, ranges and placeholders are unchanged but
// the real days are not in it. Titles, credits and ISBNs stay; they are what the parsing
// tests are about. The counts asserted against it are the ones the real run produced.
internal static class StoryGraphExportFixture
{
    public const string FileName = "story-graph-export.csv";

    /// <summary>Books in the real export.</summary>
    public const int ExpectedBookCount = 76;

    /// <summary>Distinct series those books collapse into.</summary>
    public const int ExpectedGroupCount = 11;

    /// <summary>The export as the test run sees it on disk.</summary>
    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "Features", "Import", "Fixtures", FileName);

    /// <summary>The file itself, for tests that feed the importer rather than the reader.</summary>
    public static Stream OpenRead() => File.OpenRead(FilePath);

    public static async Task<IReadOnlyList<ImportedBook>> ReadAsync()
    {
        await using var stream = OpenRead();

        var reader = new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance);
        return await reader.ReadAsync(stream, CancellationToken.None);
    }

    /// <summary>The books of one series, keyed the way the importer groups them.</summary>
    public static async Task<IReadOnlyList<ImportedBook>> ReadGroupAsync(string seriesKey)
    {
        var books = await ReadAsync();
        return [.. books.Where(book => book.SeriesKey == seriesKey)];
    }
}
