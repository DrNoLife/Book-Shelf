namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class StoryGraphImporter(
    StoryGraphCsvReader csvReader,
    ImportBatchBuilder batchBuilder) : IShelfImporter
{
    public string SourceName => "StoryGraph";

    /// <summary>
    /// "Read Status" and "Dates Read" together: StoryGraph writes both, and no
    /// other export the shelf reads writes either.
    /// </summary>
    // Two columns rather than one, because a single common word is the kind of
    // rule that quietly claims someone else's file.
    public bool CanRead(string headerLine)
    {
        ArgumentNullException.ThrowIfNull(headerLine);

        return headerLine.Contains("Read Status", StringComparison.OrdinalIgnoreCase)
               && headerLine.Contains("Dates Read", StringComparison.OrdinalIgnoreCase);
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
