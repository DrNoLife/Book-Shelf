namespace LnShelf.Api.Features.Recommendations.Services;

/// <summary>
/// Turns seeds and what readers of each also liked into "Because you read X" rows.
/// </summary>
// Backlog §11: a candidate's score is the sum over seeds of
//   weight x ln(1 + votes) / ln(1 + the seed's top votes).
// Normalising per seed stops a seed with huge vote counts from drowning the rest. Dropped
// seeds carry a negative weight, so they subtract: a neighbour of something the reader
// abandoned sinks, and disappears when its net is not positive.
public static class ForYouRanker
{
    private const int PicksPerRow = 8;
    private const int MaximumRows = 6;

    public static IReadOnlyList<ForYouRow> Rank(
        IReadOnlyList<RecommendationSeed> seeds,
        IReadOnlyDictionary<int, SeriesRecommendations> recommendations,
        IReadOnlySet<int> excluded)
    {
        var candidates = new Dictionary<int, Candidate>();

        foreach (var seed in seeds)
        {
            if (!recommendations.TryGetValue(seed.ExternalId, out var entry) || entry.Works.Count == 0)
            {
                continue;
            }

            // Measured before exclusions: a seed whose top pick is already shelved still
            // normalises against it. The source's filter guarantees at least 2 votes, so the
            // logarithm is positive.
            var topLogarithm = Math.Log(1 + entry.Works.Max(work => work.Votes));

            foreach (var work in entry.Works.Where(work => !excluded.Contains(work.ExternalId)))
            {
                var contribution = seed.Weight * Math.Log(1 + work.Votes) / topLogarithm;
                if (!candidates.TryGetValue(work.ExternalId, out var candidate))
                {
                    candidate = new Candidate(work);
                    candidates[work.ExternalId] = candidate;
                }

                candidate.Add(seed, contribution);
            }
        }

        return candidates.Values
            .Where(candidate => candidate.Total > 0 && candidate.BestSeed is not null)
            .GroupBy(candidate => candidate.BestSeed!)
            .Select(group => new ForYouRow(
                group.Key,
                group
                    .OrderByDescending(candidate => candidate.Total)
                    .ThenByDescending(candidate => candidate.Work.Votes)
                    .ThenBy(candidate => candidate.Work.ExternalId)
                    .Take(PicksPerRow)
                    .Select(candidate => new ForYouPick(candidate.Work, candidate.Total, candidate.PositiveSeedCount - 1))
                    .ToList()))
            .OrderByDescending(row => row.Picks[0].Score)
            .ThenBy(row => row.Seed.Title, StringComparer.Ordinal)
            .Take(MaximumRows)
            .ToList();
    }

    private sealed class Candidate(RecommendedWork work)
    {
        private double _bestContribution;

        public RecommendedWork Work { get; } = work;

        public double Total { get; private set; }

        public RecommendationSeed? BestSeed { get; private set; }

        public int PositiveSeedCount { get; private set; }

        public void Add(RecommendationSeed seed, double contribution)
        {
            Total += contribution;
            if (contribution <= 0)
            {
                return;
            }

            PositiveSeedCount++;
            if (contribution > _bestContribution)
            {
                _bestContribution = contribution;
                BestSeed = seed;
            }
        }
    }
}
