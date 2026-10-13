namespace LnShelf.Api.Features.Recommendations.Services;

/// <summary>
/// How much each shelf series counts as a taste signal for For you (backlog §11): status,
/// how far the reader got, and how recently.
/// </summary>
public static class SeedWeights
{
    private const double CompletedFactor = 1.0;
    private const double ReadingFactor = 0.8;

    // Negative on purpose: a Dropped series is a signal against what is close to it.
    private const double DroppedFactor = -0.5;

    private const double HalfLifeDays = 365;

    // A series with no date at all counts as a year old, so it is neither fresh nor forgotten.
    private const double UndatedAgeDays = 365;

    private const int DepthCap = 15;

    // 40 + 10 = 50, one AniList page: a cold For you load is one request.
    public const int MaximumPositiveSeeds = 40;
    public const int MaximumDroppedSeeds = 10;

    /// <returns>The series' weight, or null when it is not a seed at all.</returns>
    public static double? WeightOf(ShelfSeedFacts facts, DateOnly today)
    {
        if (facts.ExternalId is null || facts.Status == TrackingStatus.PlanToStart)
        {
            return null;
        }

        if (facts.Status == TrackingStatus.Reading && facts.ReadVolumes == 0 && !facts.HasChapterProgress)
        {
            return null;
        }

        // 1.0 also covers Completed with nothing ticked: the reader said they finished it.
        var depth = facts.ReadVolumes > 0 ? Math.Log2(1 + Math.Min(facts.ReadVolumes, DepthCap)) : 1.0;

        var lastDate = facts.LastReadAt ?? facts.NewestVolumeReadAt ?? facts.StartedAt;
        var ageDays = lastDate is null ? UndatedAgeDays : Math.Max(0, today.DayNumber - lastDate.Value.DayNumber);
        var recency = Math.Pow(0.5, ageDays / HalfLifeDays);

        return facts.Status switch
        {
            TrackingStatus.Completed => CompletedFactor * depth * recency,
            TrackingStatus.Reading => ReadingFactor * depth * recency,
            // Depth is ignored: how far a reader got before dropping says little.
            TrackingStatus.Dropped => DroppedFactor * recency,
            _ => null,
        };
    }

    public static IReadOnlyList<RecommendationSeed> Choose(IEnumerable<ShelfSeedFacts> shelf, DateOnly today)
    {
        var seeds = shelf
            .Select(facts => (Facts: facts, Weight: WeightOf(facts, today)))
            .Where(candidate => candidate.Weight is not null)
            .Select(candidate => new RecommendationSeed(
                candidate.Facts.SeriesId,
                candidate.Facts.ExternalId!.Value,
                candidate.Facts.Title,
                candidate.Facts.Format,
                candidate.Weight!.Value))
            .ToList();

        var positive = seeds
            .Where(seed => seed.Weight > 0)
            .OrderByDescending(seed => seed.Weight)
            .ThenBy(seed => seed.SeriesId)
            .Take(MaximumPositiveSeeds);

        // Most negative first: the most recently dropped says the most about taste now.
        var dropped = seeds
            .Where(seed => seed.Weight < 0)
            .OrderBy(seed => seed.Weight)
            .ThenBy(seed => seed.SeriesId)
            .Take(MaximumDroppedSeeds);

        return [.. positive, .. dropped];
    }
}
