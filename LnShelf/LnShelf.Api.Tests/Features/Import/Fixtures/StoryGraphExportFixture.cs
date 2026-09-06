namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// The real StoryGraph export, read through the production reader.
/// </summary>
// testing.md #2: every mismatch found during the real import becomes a regression
// case. This is that file — 76 books, linked from docs/ rather than copied — and
// the numbers asserted against it are the ones the real run produced.
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
