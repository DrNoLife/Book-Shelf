using CsvHelper;
using CsvHelper.Configuration;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Reads and writes the ISBN review file — the artefact between discovery and
/// applying anything.
/// </summary>
// A CSV rather than a table, because the reviewer is a person with a spreadsheet and
// the review is "delete the rows you do not believe". Nothing in the app reads it
// except the apply command, so it has no business being schema.
public sealed class IsbnReviewFile(ILogger<IsbnReviewFile> logger)
{
    private static readonly CsvConfiguration Configuration = new(CultureInfo.InvariantCulture)
    {
        // Forgiving on read for the same reason the StoryGraph reader is: this file
        // comes back from a spreadsheet, which may well have reordered or dropped a
        // column the reviewer had no use for.
        HeaderValidated = null,
        MissingFieldFound = null,
        DetectDelimiter = false,
    };

    public async Task WriteAsync(
        string path,
        IReadOnlyList<IsbnDiscoveryRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(rows);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        await using var csvWriter = new CsvWriter(writer, Configuration);

        await csvWriter.WriteRecordsAsync(rows.Select(row => row.ToCsvRow()), cancellationToken);

        logger.LogInformation(
            "Wrote {RowCount} row(s) to {Path}, {ProposedCount} of them carrying a proposed ISBN",
            rows.Count,
            Path.GetFullPath(path),
            rows.Count(row => row.Isbn is not null));
    }

    /// <summary>
    /// The rows that are an instruction: a real series, a real volume, a real ISBN.
    /// </summary>
    // Rows with no ISBN are the discovery failures, left in the file so it does not
    // read as a complete list. They are skipped here rather than treated as "clear
    // this volume's ISBN" — the apply command only ever adds.
    public async Task<IReadOnlyList<IsbnReviewInstruction>> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No review file at '{Path.GetFullPath(path)}'.", path);
        }

        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csvReader = new CsvReader(reader, Configuration);

        var instructions = new List<IsbnReviewInstruction>();
        var skipped = 0;

        await foreach (var csvRow in csvReader.GetRecordsAsync<IsbnReviewCsvRow>(cancellationToken))
        {
            if (csvRow.ToInstruction() is { } instruction)
            {
                instructions.Add(instruction);
            }
            else
            {
                skipped++;
            }
        }

        logger.LogInformation(
            "Read {InstructionCount} reviewed ISBN(s) from {Path}, {SkippedCount} row(s) without one",
            instructions.Count,
            Path.GetFullPath(path),
            skipped);

        return instructions;
    }
}
