namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class AniListSeriesMatcher(
    ISeriesMetadataSource metadataSource,
    IOptions<ImportOptions> options,
    ILogger<AniListSeriesMatcher> logger) : ISeriesMatcher
{
    public async Task<SeriesMatch> MatchAsync(
        string seriesTitle,
        string? author,
        string? illustrator,
        CancellationToken cancellationToken)
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

        var match = Decide(candidates, author, illustrator);

        logger.LogInformation(
            "Matched {SeriesTitle}: {CandidateCount} candidates, {Status} ({Reason}), top score {TopScore}",
            seriesTitle,
            candidates.Count,
            match.Status,
            match.Reason,
            match.MatchScore ?? candidates.FirstOrDefault()?.Score ?? 0);

        return match;
    }

    private SeriesMatch Decide(
        List<ScoredSeriesCandidate> candidates,
        string? author,
        string? illustrator)
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

            if (reason is ImportReviewReason.FormatAmbiguous
                && BreakFormatTie(candidates, author, illustrator) is { } decided)
            {
                // Logged separately because the summary line will call this an
                // ordinary auto-match: a decision taken on a credit rather than on
                // a title is the one worth being able to find afterwards.
                logger.LogInformation(
                    "Broke a format tie on credits: {AniListId} ({Format}) — author {Author}, illustrator {Illustrator}",
                    decided.Result.AniListId,
                    decided.Result.Format,
                    author,
                    illustrator);

                return AutoMatched(candidates, decided);
            }

            return NeedsReview(candidates, reason);
        }

        return AutoMatched(candidates, best);
    }

    /// <summary>
    /// The one candidate the export's own credits point at, or null when they
    /// point at none or at several.
    /// </summary>
    // Only ever applied to a FormatAmbiguous tie, and never to the other three
    // decisions: this is not a general scoring signal, it is the answer to the one
    // question the title cannot answer — "the novel or the adaptation?". The
    // adaptation credits the novelist as Story, so the artist is tried first and the
    // author only as a fallback for the sources that publish no artist at all.
    //
    // Contenders rather than every candidate: a spin-off forty points behind did not
    // cause this tie and must not win it just because its artist happens to match.
    private ScoredSeriesCandidate? BreakFormatTie(
        List<ScoredSeriesCandidate> candidates,
        string? author,
        string? illustrator)
    {
        var contenders = candidates
            .Where(candidate => candidates[0].Score - candidate.Score < options.Value.AutoMatchMinimumMargin)
            .ToList();

        return SingleMatch(contenders, illustrator, candidate => candidate.Result.Illustrator)
               ?? SingleMatch(contenders, author, candidate => candidate.Result.Author);
    }

    /// <summary>
    /// The single contender whose credit is this person, or null when none or
    /// more than one is.
    /// </summary>
    // "More than one" is a real outcome, not a defect: a one-person mangaka is
    // credited as both author and artist on every edition of their work, so the
    // author test in particular can match two candidates at once. Two matches is
    // still a tie, and a tie goes to the user.
    private static ScoredSeriesCandidate? SingleMatch(
        List<ScoredSeriesCandidate> contenders,
        string? credited,
        Func<ScoredSeriesCandidate, string?> credit)
    {
        var matches = contenders
            .Where(candidate => PersonNameNormalizer.AreSamePerson(credited, credit(candidate)))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static SeriesMatch AutoMatched(
        List<ScoredSeriesCandidate> candidates,
        ScoredSeriesCandidate chosen) => new()
        {
            Candidates = candidates,
            Status = ImportGroupStatus.AutoMatched,
            Reason = ImportReviewReason.None,
            MatchedAniListId = chosen.Result.AniListId,
            MatchScore = chosen.Score,
        };

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
