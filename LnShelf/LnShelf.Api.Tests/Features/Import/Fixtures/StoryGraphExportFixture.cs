using Microsoft.Extensions.Logging.Abstractions;

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

    public static async Task<IReadOnlyList<StoryGraphBook>> ReadAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Features", "Import", "Fixtures", FileName);
        await using var stream = File.OpenRead(path);

        var reader = new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance);
        return await reader.ReadAsync(stream, CancellationToken.None);
    }

    /// <summary>The books of one series, keyed the way the importer groups them.</summary>
    public static async Task<IReadOnlyList<StoryGraphBook>> ReadGroupAsync(string seriesKey)
    {
        var books = await ReadAsync();
        return [.. books.Where(book => book.SeriesKey == seriesKey)];
    }
}
