namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// AniList is the preferred source and stays so: it has the English titles, the romaji and
// native ones, the staff and a real volume count, and MangaUpdates has none of that in a
// search payload. But on 2026-08-15 AniList disabled its entire public GraphQL API —
// every POST answered 403 in ten milliseconds — and with it this shelf could neither
// search for nor add a single series, while MangaUpdates answered normally throughout.
//
// So the order is preference, not dependence. What decides a substitution is
// MetadataSourceFailure, not a catch-all: being rate-limited or asking a malformed query
// must not move traffic onto a second free API.
public sealed class FallbackSeriesSearchService(
    ISeriesMetadataSource preferredSource,
    IMangaUpdatesSeriesQueryService fallbackSource,
    ILogger<FallbackSeriesSearchService> logger) : ISeriesSearchService
{
    private const MetadataSourceName PreferredSourceName = MetadataSourceName.AniList;
    private const MetadataSourceName FallbackSourceName = MetadataSourceName.MangaUpdates;

    /// <summary>
    /// The one quiet line the search page prints when the substitution happened.
    /// </summary>
    // Stated rather than hidden, and stated once (design.md's voice rules): results that
    // silently changed source would explain neither the romaji titles nor the missing
    // volume counts the reader is about to see.
    public static string DegradedNote =>
        $"{PreferredSourceName} is unavailable — results from {FallbackSourceName}.";

    public async Task<SeriesSearchOutcome> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken)
    {
        try
        {
            return new SeriesSearchOutcome
            {
                Source = PreferredSourceName,
                Results = await preferredSource.SearchAsync(query, format, cancellationToken),
            };
        }
        catch (Exception exception) when (MetadataSourceFailure.WarrantsFallback(exception, cancellationToken))
        {
            logger.LogWarning(
                exception,
                "{PreferredSource} could not answer the search for {Query}; asking {FallbackSource}",
                PreferredSourceName,
                query,
                FallbackSourceName);

            return await FallBackAsync(query, format, exception, cancellationToken);
        }
    }

    private async Task<SeriesSearchOutcome> FallBackAsync(
        string query,
        Format? format,
        Exception preferredFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await fallbackSource.SearchAsync(query, format, cancellationToken);

            logger.LogInformation(
                "{FallbackSource} answered the search for {Query} with {ResultCount} result(s) "
                + "while {PreferredSource} is down",
                FallbackSourceName,
                query,
                results.Count,
                PreferredSourceName);

            return new SeriesSearchOutcome
            {
                Source = FallbackSourceName,
                Results = results,
                DegradedNote = DegradedNote,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Named for the search rather than for one of the two sources: with both of
            // them refusing, "AniList is unavailable" would be half the story and the
            // reader's actual situation is that searching does not work at all.
            throw new MetadataSourceUnavailableException(
                "Series search",
                $"Neither {PreferredSourceName} nor {FallbackSourceName} could answer. "
                + $"{PreferredSourceName}: {preferredFailure.Message} "
                + $"{FallbackSourceName}: {exception.Message}",
                innerException: exception);
        }
    }
}
