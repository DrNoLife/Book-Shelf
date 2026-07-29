namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class AniListSeriesMatcher(
    ISeriesMetadataSource metadataSource,
    IOptions<ImportOptions> options,
    ILogger<AniListSeriesMatcher> logger) : ISeriesMatcher
{
    public async Task<SeriesMatch> MatchAsync(string seriesTitle, CancellationToken cancellationToken)
    {
        // One search per distinct series title. The 76 books of a real export
        // collapse to 11 titles before anything is asked of AniList, and the
        // search cache makes a repeated title free on top of that.
        var results = await metadataSource.SearchAsync(seriesTitle, format: null, cancellationToken);

        // OrderByDescending is a stable sort, so candidates that tie on score keep
        // the order AniList returned them in — which is its own relevance order.
        // Nothing here reorders a tie, because a tie is exactly what must not be
        // silently broken.
        var candidates = results
            .Select(result => new ScoredSeriesCandidate
            {
                Result = result,
                Score = TitleSimilarityScorer.BestScore(
                    seriesTitle, result.TitleEnglish, result.TitleRomaji, result.TitleNative),
            })
            .Where(candidate => candidate.Score >= options.Value.CandidateMinimumScore)
            .OrderByDescending(candidate => candidate.Score)
            .Take(options.Value.CandidatesPerGroup)
            .ToList();

        var match = Decide(candidates);

        logger.LogInformation(
            "Matched {SeriesTitle}: {CandidateCount} candidates, {Status} ({Reason}), top score {TopScore}",
            seriesTitle,
            candidates.Count,
            match.Status,
            match.Reason,
            match.MatchScore ?? candidates.FirstOrDefault()?.Score ?? 0);

        return match;
    }

    private SeriesMatch Decide(List<ScoredSeriesCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return NeedsReview(candidates, ImportReviewReason.NoCandidate);
        }

        var best = candidates[0];

        if (best.Score < options.Value.AutoMatchMinimumScore)
        {
            return NeedsReview(candidates, ImportReviewReason.LowConfidence);
        }

        var runnerUp = candidates.Count > 1 ? candidates[1] : null;
        var margin = best.Score - (runnerUp?.Score ?? 0);

        if (margin < options.Value.AutoMatchMinimumMargin)
        {
            // The usual outcome for a book export, and the reason this queue
            // exists: on AniList a light novel and its manga adaptation carry the
            // same title, and nothing in a StoryGraph row says which medium was
            // read. Picking one would be inventing the user's own history.
            var reason = runnerUp!.Result.Format != best.Result.Format
                ? ImportReviewReason.FormatAmbiguous
                : ImportReviewReason.AmbiguousCandidates;

            return NeedsReview(candidates, reason);
        }

        return new SeriesMatch
        {
            Candidates = candidates,
            Status = ImportGroupStatus.AutoMatched,
            Reason = ImportReviewReason.None,
            MatchedAniListId = best.Result.ExternalId,
            MatchScore = best.Score,
        };
    }

    private static SeriesMatch NeedsReview(
        List<ScoredSeriesCandidate> candidates,
        ImportReviewReason reason) => new()
        {
            Candidates = candidates,
            Status = ImportGroupStatus.NeedsReview,
            Reason = reason,
            MatchedAniListId = null,

            // The top score is still recorded: it is what the queue prints to
            // explain why this needed a person.
            MatchScore = candidates.Count > 0 ? candidates[0].Score : null,
        };
}
