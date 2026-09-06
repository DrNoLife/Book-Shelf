namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Picks the one <see cref="IShelfImporter"/> that recognises an uploaded file,
/// by its header row.
/// </summary>
// This is what makes IShelfImporter a real seam rather than a name: every
// implementation is registered under it, so the controller cannot inject "the"
// importer and a second registration cannot silently replace the first.
// Registering a new source is the whole of adding one.
public sealed class ImportSourceResolver(IEnumerable<IShelfImporter> importers)
{
    /// <summary>
    /// Reads the first line of <paramref name="content"/> and rewinds it, so the
    /// chosen importer still sees the whole file.
    /// </summary>
    /// <exception cref="InvalidInputException">
    /// The upload cannot be seeked to sniff its header, no importer recognises the
    /// header, or more than one does.
    /// </exception>
    public async Task<IShelfImporter> ResolveAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        // A rewind rather than a copy: an 8 MB upload is already buffered by
        // ASP.NET Core, and buffering it a second time to read one line would be
        // paying twice for the same bytes. A non-seekable stream only reaches here
        // through a caller outside that buffering (a test, or a future non-HTTP
        // caller), so it is the request that is malformed, not the server —
        // InvalidInputException, not an ArgumentException that would surface as a 500.
        if (!content.CanSeek)
        {
            throw new InvalidInputException("That upload could not be read; try sending the file again.");
        }

        var headerLine = await ReadHeaderLineAsync(content, cancellationToken);
        content.Position = 0;

        var recognised = importers.Where(importer => importer.CanRead(headerLine)).ToList();

        return recognised.Count switch
        {
            1 => recognised[0],

            0 => throw new InvalidInputException(
                $"That file's first line is not an export LnShelf knows how to read. Supported exports: {SupportedSources()}."),

            // Two sources claiming one file means their sniffing rules overlap,
            // which is a defect in them rather than in the file — but guessing
            // between two readers would stage the user's history through the
            // wrong one, so it refuses and says so.
            _ => throw new InvalidInputException(
                $"That file's first line matches more than one supported export ({string.Join(", ", recognised.Select(importer => importer.SourceName).Order(StringComparer.Ordinal))}), so LnShelf cannot tell which it is. Supported exports: {SupportedSources()}."),
        };
    }

    private static async Task<string> ReadHeaderLineAsync(Stream content, CancellationToken cancellationToken)
    {
        // leaveOpen, because the importer reads this same stream next.
        // detectEncodingFromByteOrderMarks strips the UTF-8 BOM StoryGraph writes,
        // which would otherwise sit in front of the first column name.
        using var reader = new StreamReader(
            content,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        return await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
    }

    private string SupportedSources() =>
        string.Join(", ", importers.Select(importer => importer.SourceName).Order(StringComparer.Ordinal));
}
