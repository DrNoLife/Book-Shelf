using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Reads a StoryGraph export into clean books.
/// </summary>
public sealed class StoryGraphCsvReader(ILogger<StoryGraphCsvReader> logger)
{
    // Forgiving on purpose. This parses a file exported by someone else's product:
    // a column that moved, a header that gained a word, or a row with a ragged
    // field count must not throw away the other 75 rows. What it must never do is
    // silently invent data, which is why every skipped row is logged.
    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        HeaderValidated = null,
        MissingFieldFound = null,
        BadDataFound = null,
        DetectDelimiter = false,

        // The export really does contain "オーバーロード1 不死者の王 " — a trailing
        // space inside the quotes.
        TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes,
    };

    public async Task<IReadOnlyList<StoryGraphBook>> ReadAsync(Stream csv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);

        // detectEncodingFromByteOrderMarks handles the UTF-8 BOM StoryGraph writes.
        using var reader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csvReader = new CsvReader(reader, Configuration);

        var books = new List<StoryGraphBook>();
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
            "Read {BookCount} books from the export, {SkippedCount} rows skipped", books.Count, skipped);

        return books;
    }
}
