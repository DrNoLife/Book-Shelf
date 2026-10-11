namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// Through IReleaseSource rather than MangaUpdates by name, so the request shares the cached,
// rate-limited search the sync's matcher already uses (MangaUpdatesSearchCache) and nothing
// here knows which source answers.
public sealed class ReleaseLinkQueryService(
    LnShelfDbContext dbContext,
    IReleaseSource releaseSource) : IReleaseLinkQueryService
{
    /// <summary>Longer than any title the shelf holds; a longer query is not a title.</summary>
    public const int MaximumQueryLength = 200;

    public async Task<ReleaseLinkCandidatesResponse?> GetCandidatesAsync(
        Guid seriesId,
        string? query,
        CancellationToken cancellationToken)
    {
        if (query is { Length: > MaximumQueryLength })
        {
            throw new InvalidInputException($"Search for at most {MaximumQueryLength} characters.");
        }

        // Owner-filtered: another user's series answers as if it did not exist.
        var series = await dbContext.Series
            .AsNoTracking()
            .Where(candidate => candidate.Id == seriesId)
            .Select(candidate => new { candidate.TitleEnglish, candidate.TitleRomaji, candidate.Format })
            .FirstOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return null;
        }

        // Romaji first when nothing was typed, for the matcher's reason: MangaUpdates files
        // light novels under their transliterated titles, and the English name returns pages
        // of unrelated hits.
        var searched = query.NullIfBlank() ?? series.TitleRomaji.NullIfBlank() ?? series.TitleEnglish;

        IReadOnlyList<ReleaseSeriesCandidate> candidates;

        try
        {
            candidates = await releaseSource.SearchAsync(searched, series.Format, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw ReleaseSourceUnavailable.From(releaseSource.SourceName, exception);
        }
        catch (Exception exception) when (exception is OperationCanceledException or TimeoutException
                                           && !cancellationToken.IsCancellationRequested)
        {
            throw ReleaseSourceUnavailable.FromTimeout(releaseSource.SourceName, exception);
        }

        // Every other series' link, so a record already spoken for is marked rather than
        // offered as if it were free; the command refuses it either way.
        var linkedElsewhere = await dbContext.Series
            .AsNoTracking()
            .Where(candidate => candidate.MangaUpdatesId != null && candidate.Id != seriesId)
            .Select(candidate => new LinkedSeries(candidate.Id, candidate.MangaUpdatesId!.Value, candidate.TitleEnglish))
            .ToListAsync(cancellationToken);

        var byRecord = linkedElsewhere
            .GroupBy(linked => linked.MangaUpdatesId)
            .ToDictionary(group => group.Key, group => group.First());

        return new ReleaseLinkCandidatesResponse
        {
            Query = searched,
            Candidates =
            [
                .. candidates.Select(candidate =>
                    ReleaseLinkMapper.ToResponse(candidate, byRecord.GetValueOrDefault(candidate.ExternalId))),
            ],
        };
    }
}
