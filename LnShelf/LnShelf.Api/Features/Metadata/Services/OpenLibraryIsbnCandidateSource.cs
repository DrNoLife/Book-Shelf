using LnShelf.Api.Features.Metadata.Models.OpenLibrary;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class OpenLibraryIsbnCandidateSource(
    OpenLibraryClient client,
    IOptions<OpenLibraryOptions> options,
    ILogger<OpenLibraryIsbnCandidateSource> logger) : IIsbnCandidateSource
{
    public string SourceName => "OpenLibrary";

    /// <summary>
    /// The query forms Open Library's own titles actually take.
    /// </summary>
    // This used to send a single quoted phrase, "<series> volume <N>", and it was the
    // reason enrichment reported volumes as uncatalogued that Open Library plainly has.
    // Its search ANDs the terms and no title here contains the word "volume", so that one
    // token excluded every result. Measured against the real catalogue:
    //
    //   "…Slime volume 16"   0 hits      <- what was being sent
    //   …Slime volume 16     0 hits      <- the word alone is fatal, quotes or not
    //   …Slime 16            2 hits      <- "That Time I Got Reincarnated as a Slime 16"
    //   "…Slime, Vol. 16"    1 hit       <- the Japanese light-novel record
    //
    // So both working forms are tried and their ISBNs unioned: Open Library titles the
    // English editions "<series> N" and the light-novel editions "<series>, Vol. N", and
    // neither form finds the other. Two requests a volume is the price of that.
    private static IEnumerable<string> QueriesFor(string seriesTitle, int volumeNumber)
    {
        // Bare number, unquoted. Loose on purpose — every candidate is verified by ISBN
        // afterwards, and VolumeEditionMatcher throws out anything that is not this
        // series' volume N.
        yield return $"{seriesTitle} {volumeNumber}";

        // Quoted, because "Vol." unquoted matches every volume of everything.
        yield return $"\"{seriesTitle}, Vol. {volumeNumber}\"";
    }

    public async Task<IsbnCandidates> FindAsync(
        string seriesTitle,
        int volumeNumber,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesTitle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(volumeNumber);

        var worksFound = 0;
        var isbns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var query in QueriesFor(seriesTitle, volumeNumber))
        {
            // One query form failing must not cost the volume its other form. Open Library's
            // search is a Solr query and is genuinely slow: a real run over nine volumes lost
            // three of them outright to timeouts, and two of those three were findable. This
            // is the same per-item isolation the sync run uses one level up.
            List<OpenLibrarySearchDocument> documents;

            try
            {
                var response = await client.SearchAsync(query, cancellationToken);
                documents = response?.Docs ?? [];
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Open Library search {Query} timed out; trying the next form", query);
                continue;
            }
            catch (Exception exception) when (exception is HttpRequestException or TimeoutException)
            {
                logger.LogWarning(
                    exception, "Open Library search {Query} failed; trying the next form", query);
                continue;
            }

            worksFound += documents.Count;

            // The union of every hit's ISBN list, and never a hit's title. A search groups
            // all editions of a work into a single hit whose title is whichever Open
            // Library considers primary — for this shelf's series, often the Japanese one —
            // and the language filter cannot narrow it either, because the hit's language
            // array is the union across editions. Both measured, both falsified as
            // shortcuts (.research/volume-enrichment/e-isbn-first-pipeline.md).
            foreach (var candidate in documents.SelectMany(document => document.Isbn ?? []))
            {
                if (Isbn.Normalize(candidate) is { } normalized && seen.Add(normalized))
                {
                    isbns.Add(normalized);
                }
            }

            logger.LogInformation(
                "Open Library search {Query} returned {WorkCount} work(s); {IsbnCount} candidate ISBN(s) so far",
                query,
                documents.Count,
                isbns.Count);

            if (isbns.Count >= options.Value.CandidateIsbnLimit)
            {
                // Enough to verify. Asking the second form as well would spend a request
                // to lengthen a list that is already being truncated.
                break;
            }
        }

        return new IsbnCandidates
        {
            WorksFound = worksFound,
            Isbns = [.. isbns.Take(options.Value.CandidateIsbnLimit)],
        };
    }
}
