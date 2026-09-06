namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
// The second IShelfImporter, and the proof the seam holds: nothing outside this
// file, its reader, its row class and its mapper knows Goodreads exists. The
// grouping, matching, cover caching and staging are ImportBatchBuilder's, exactly
// as they are for StoryGraph.
public sealed class GoodreadsImporter(
    GoodreadsCsvReader csvReader,
    ImportBatchBuilder batchBuilder) : IShelfImporter
{
    public string SourceName => "Goodreads";

    /// <summary>
    /// "Exclusive Shelf" and "Book Id" together: Goodreads writes both, and no
    /// other export the shelf reads writes either.
    /// </summary>
    public bool CanRead(string headerLine)
    {
        ArgumentNullException.ThrowIfNull(headerLine);

        return headerLine.Contains("Exclusive Shelf", StringComparison.OrdinalIgnoreCase)
               && headerLine.Contains("Book Id", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ImportBatchResponse> CreateBatchAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var books = await csvReader.ReadAsync(content, cancellationToken);

        return await batchBuilder.BuildAsync(SourceName, fileName, books, cancellationToken);
    }
}
