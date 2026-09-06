using CsvHelper;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Reads a StoryGraph export into clean books.
/// </summary>
public sealed class StoryGraphCsvReader(ILogger<StoryGraphCsvReader> logger)
{
    public async Task<IReadOnlyList<ImportedBook>> ReadAsync(Stream csv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);

        // detectEncodingFromByteOrderMarks handles the UTF-8 BOM StoryGraph writes.
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csvReader = new CsvReader(reader, ExportCsvConfiguration.Forgiving);

        var books = new List<ImportedBook>();
        var lineNumber = 1;
        var skipped = 0;

        await foreach (var row in csvReader.GetRecordsAsync<StoryGraphCsvRow>(cancellationToken))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(row.Title))
            {
                skipped++;
                logger.LogWarning("Import line {LineNumber} has no title; skipped", lineNumber);
                continue;
            }

            books.Add(StoryGraphBookMapper.ToBook(row, lineNumber));
        }

        logger.LogInformation(
            "Read {BookCount} books from the StoryGraph export, {SkippedCount} rows skipped", books.Count, skipped);

        return books;
    }
}
