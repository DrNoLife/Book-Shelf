namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class OpenLibraryIsbnCandidateSource(
    OpenLibraryClient client,
    IOptions<OpenLibraryOptions> options,
    ILogger<OpenLibraryIsbnCandidateSource> logger) : IIsbnCandidateSource
{
    public string SourceName => "OpenLibrary";

    public async Task<IsbnCandidates> FindAsync(
        string seriesTitle,
        int volumeNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesTitle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(volumeNumber);

        // The phrase form the measurement used. A quoted phrase, because unquoted it
        // returns an 1887 religious tract for "86 volume 12".
        var query = $"\"{seriesTitle} volume {volumeNumber}\"";

        var response = await client.SearchAsync(query, cancellationToken);
        var documents = response?.Docs ?? [];

        // The union of every hit's ISBN list, and not one hit's title. A search groups
        // all editions of a work into a single hit whose title is whichever Open Library
        // considers primary — for this shelf's series, usually the Japanese one — and
        // the language filter cannot narrow it either, because the hit's language array
        // is the union across editions. Both measured, both falsified as shortcuts
        // (.research/volume-enrichment/e-isbn-first-pipeline.md).
        var isbns = documents
            .SelectMany(document => document.Isbn ?? [])
            .Select(Isbn.Normalize)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(options.Value.CandidateIsbnLimit)
            .ToList();

        logger.LogInformation(
            "Open Library search {Query} returned {WorkCount} work(s) offering {IsbnCount} candidate ISBN(s)",
            query,
            documents.Count,
            isbns.Count);

        return new IsbnCandidates { WorksFound = documents.Count, Isbns = isbns };
    }
}
