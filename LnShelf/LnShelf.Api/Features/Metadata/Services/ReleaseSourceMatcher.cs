namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class ReleaseSourceMatcher(
    IReleaseSource releaseSource,
    IOptions<MangaUpdatesOptions> options,
    ILogger<ReleaseSourceMatcher> logger) : IReleaseSourceMatcher
{
    public async Task<ReleaseSourceMatch> MatchAsync(
        Series series,
        IReadOnlySet<long> alreadyLinkedExternalIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(alreadyLinkedExternalIds);

        // Searched on romaji first: the release source files light novels under
        // their transliterated Japanese titles ("Youjo Senki", "Honzuki no
        // Gekokujou"), and searching those finds the series where searching the
        // English name returns two thousand unrelated hits.
        var candidates = await FindCandidatesAsync(series, cancellationToken);

        if (candidates.Count == 0)
        {
            return NotLinked(
                ReleaseMatchOutcome.NoCandidate,
                score: null,
                $"{releaseSource.SourceName} returned nothing for '{series.TitleEnglish}'.");
        }

        var scored = await ScoreAsync(series, candidates, cancellationToken);
        if (scored.Count == 0)
        {
            return NotLinked(
                ReleaseMatchOutcome.NoCandidate,
                score: null,
                $"None of {releaseSource.SourceName}'s candidates could be fetched for scoring.");
        }

        var best = scored[0];
        var runnerUp = scored.Count > 1 ? scored[1] : null;

        if (best.Score < options.Value.AutoLinkMinimumScore)
        {
            return NotLinked(
                ReleaseMatchOutcome.LowConfidence,
                best.Score,
                $"Closest was '{best.Info.Title}' at {best.Score}; below {options.Value.AutoLinkMinimumScore}.");
        }

        if (runnerUp is not null && best.Score - runnerUp.Score < options.Value.AutoLinkMinimumMargin)
        {
            return NotLinked(
                ReleaseMatchOutcome.Ambiguous,
                best.Score,
                $"'{best.Info.Title}' and '{runnerUp.Info.Title}' both scored around {best.Score}.");
        }

        if (alreadyLinkedExternalIds.Contains(best.Info.ExternalId))
        {
            // Not an error and not a near miss: the sources model the series
            // differently, and no automatic answer exists. Left for a person.
            return NotLinked(
                ReleaseMatchOutcome.AlreadyLinked,
                best.Score,
                $"'{best.Info.Title}' ({best.Info.ExternalId}) is already linked to another series on the shelf.");
        }

        logger.LogInformation(
            "Linked {TitleEnglish} to {SourceName} {ExternalId} '{SourceTitle}' at {Score}",
            series.TitleEnglish,
            releaseSource.SourceName,
            best.Info.ExternalId,
            best.Info.Title,
            best.Score);

        return new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.Linked,
            MatchedExternalId = best.Info.ExternalId,
            MatchedSeries = best.Info,
            Score = best.Score,
            Explanation = $"Linked to '{best.Info.Title}' at {best.Score}.",
        };
    }

    /// <summary>
    /// Searches each title the shelf holds, in the order most likely to work, and
    /// stops as soon as one returns anything.
    /// </summary>
    private async Task<IReadOnlyList<ReleaseSeriesCandidate>> FindCandidatesAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        foreach (var title in SearchTitles(series))
        {
            var candidates = await releaseSource.SearchAsync(title, series.Format, cancellationToken);
            if (candidates.Count > 0)
            {
                return candidates;
            }
        }

        return [];
    }

    private static IEnumerable<string> SearchTitles(Series series)
    {
        if (!string.IsNullOrWhiteSpace(series.TitleRomaji))
        {
            yield return series.TitleRomaji;
        }

        if (!string.IsNullOrWhiteSpace(series.TitleEnglish))
        {
            yield return series.TitleEnglish;
        }

        if (!string.IsNullOrWhiteSpace(series.TitleNative))
        {
            yield return series.TitleNative;
        }
    }

    /// <summary>
    /// Fetches detail for the top candidates and scores every title the shelf has
    /// against every title the source has.
    /// </summary>
    // The detail fetch is not optional. Search returns primary titles only, and the
    // primary title is routinely not the recognisable one: MangaUpdates files
    // "86―EIGHTY-SIX" under the primary title "86", which scores 50 against the
    // shelf's name and 100 against its own associated titles.
    private async Task<List<ScoredReleaseCandidate>> ScoreAsync(
        Series series,
        IReadOnlyList<ReleaseSeriesCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var shelfTitles = SearchTitles(series).ToArray();
        var scored = new List<ScoredReleaseCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var info = await releaseSource.GetSeriesAsync(candidate.ExternalId, cancellationToken);
            if (info is null)
            {
                continue;
            }

            scored.Add(new ScoredReleaseCandidate(info, BestScore(shelfTitles, info)));
        }

        // Stable: candidates that tie keep the source's own relevance order.
        return [.. scored.OrderByDescending(candidate => candidate.Score)];
    }

    private static int BestScore(string[] shelfTitles, ReleaseSeriesInfo info)
    {
        var sourceTitles = new List<string?>(info.AssociatedTitles.Count + 1) { info.Title };
        sourceTitles.AddRange(info.AssociatedTitles);

        var best = 0;
        foreach (var shelfTitle in shelfTitles)
        {
            best = Math.Max(best, TitleSimilarityScorer.BestScore(shelfTitle, [.. sourceTitles]));
        }

        return best;
    }

    private ReleaseSourceMatch NotLinked(ReleaseMatchOutcome outcome, int? score, string explanation)
    {
        logger.LogInformation(
            "Left unlinked ({Outcome}): {Explanation}", outcome, explanation);

        return new ReleaseSourceMatch
        {
            Outcome = outcome,
            MatchedExternalId = null,
            Score = score,
            Explanation = explanation,
        };
    }

    private sealed record ScoredReleaseCandidate(ReleaseSeriesInfo Info, int Score);
}
