using CsvHelper;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Reads a Goodreads export into clean books.
/// </summary>
public sealed class GoodreadsCsvReader(ILogger<GoodreadsCsvReader> logger)
{
    public async Task<IReadOnlyList<ImportedBook>> ReadAsync(Stream csv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);

        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csvReader = new CsvReader(reader, ExportCsvConfiguration.Forgiving);

        var books = new List<ImportedBook>();
        var lineNumber = 1;
        var skipped = 0;

        await foreach (var row in csvReader.GetRecordsAsync<GoodreadsCsvRow>(cancellationToken))
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(row.Title))
            {
                skipped++;
                logger.LogWarning("Import line {LineNumber} has no title; skipped", lineNumber);
                continue;
            }

            books.Add(GoodreadsBookMapper.ToBook(row, lineNumber));
        }

        logger.LogInformation(
            "Read {BookCount} books from the Goodreads export, {SkippedCount} rows skipped", books.Count, skipped);

        return books;
    }
}
