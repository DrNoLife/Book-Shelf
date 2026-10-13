using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

public sealed class ForYouRankerTests
{
    private static readonly HashSet<int> NothingExcluded = [];

    [Fact]
    public void Rank_ShelvedOrDismissed_NeverAppear()
    {
        var seed = Seed(1, 1.0);
        var recommendations = Recommendations((1, [Work(10, 9), Work(11, 8), Work(12, 7)]));

        var rows = ForYouRanker.Rank([seed], recommendations, new HashSet<int> { 10, 12 });

        rows.Single().Picks.Select(pick => pick.Work.ExternalId).ShouldBe([11]);
    }

    [Fact]
    public void Rank_AWorkFromTwoSeeds_SumsAndSitsUnderTheStrongerSeedWithOtherSeedCountOne()
    {
        var weak = Seed(1, 0.5);
        var strong = Seed(2, 1.0);
        var recommendations = Recommendations(
            (1, [Work(10, 10)]),
            (2, [Work(10, 10)]));

        var rows = ForYouRanker.Rank([weak, strong], recommendations, NothingExcluded);

        var row = rows.Single();
        row.Seed.ShouldBe(strong);
        var pick = row.Picks.Single();
        pick.OtherSeedCount.ShouldBe(1);
        pick.Score.ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public void Rank_VotesAreNormalisedPerSeed()
    {
        var smallSeed = Seed(1, 1.0, "A small seed");
        var bigSeed = Seed(2, 1.0, "A big seed");
        var recommendations = Recommendations(
            (1, [Work(10, 7)]),
            (2, [Work(20, 124), Work(21, 20)]));

        var rows = ForYouRanker.Rank([smallSeed, bigSeed], recommendations, NothingExcluded);

        // Work 10 is its seed's top pick (1.0) and beats work 21 (20 votes of a 124-vote seed).
        var scores = rows.SelectMany(row => row.Picks).ToDictionary(pick => pick.Work.ExternalId, pick => pick.Score);
        scores[10].ShouldBeGreaterThan(scores[21]);
        scores[10].ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void Rank_ADroppedSeriesNeighbour_IsLoweredAndRemovedWhenItsNetIsNotPositive()
    {
        var liked = Seed(1, 1.0);
        var dropped = Seed(2, -0.5, "Dropped");
        var recommendations = Recommendations(
            (1, [Work(10, 10), Work(11, 10), Work(12, 5)]),
            (2, [Work(10, 4), Work(11, 10)]));

        var rows = ForYouRanker.Rank([liked, dropped], recommendations, NothingExcluded);

        var picks = rows.Single().Picks;
        // 12 is untouched (0.75); 10 (4 dropped votes) falls to 0.66; 11 (the dropped seed's top) to 0.5.
        picks.Select(pick => pick.Work.ExternalId).ShouldBe([12, 10, 11]);

        var heavilyDropped = Seed(3, -2.0, "Heavily dropped");
        var vanished = ForYouRanker.Rank(
            [liked, heavilyDropped],
            Recommendations((1, [Work(10, 10)]), (3, [Work(10, 10)])),
            NothingExcluded);
        vanished.ShouldBeEmpty();
    }

    [Fact]
    public void Rank_CapsEightPerRowAndSixRows()
    {
        var seeds = Enumerable.Range(1, 8).Select(number => Seed(number, 1.0 + number / 10.0, $"Seed {number}")).ToList();
        var entries = seeds
            .Select(seed => (seed.ExternalId, Works: Enumerable.Range(1, 12)
                .Select(index => Work(seed.ExternalId * 100 + index, 50 - index))
                .ToArray()))
            .ToArray();

        var rows = ForYouRanker.Rank(seeds, Recommendations(entries), NothingExcluded);

        rows.Count.ShouldBe(6);
        rows.ShouldAllBe(row => row.Picks.Count == 8);
    }

    [Fact]
    public void Rank_TopVotesCountBeforeExclusions()
    {
        var seed = Seed(1, 1.0);
        var recommendations = Recommendations((1, [Work(10, 100), Work(11, 10)]));

        var rows = ForYouRanker.Rank([seed], recommendations, new HashSet<int> { 10 });

        // 11 is normalised against the shelved 100-vote top pick, not promoted to 1.0.
        rows.Single().Picks.Single().Score.ShouldBe(Math.Log(11) / Math.Log(101), 1e-9);
    }

    [Fact]
    public void Rank_TiesAreDeterministic()
    {
        var first = Seed(1, 1.0, "Alpha");
        var second = Seed(2, 1.0, "Beta");
        var recommendations = Recommendations(
            (1, [Work(30, 5), Work(20, 5)]),
            (2, [Work(40, 5), Work(50, 5)]));

        var forward = ForYouRanker.Rank([first, second], recommendations, NothingExcluded);
        var reversed = ForYouRanker.Rank([second, first], recommendations, NothingExcluded);

        forward.Select(row => row.Seed.Title).ShouldBe(["Alpha", "Beta"]);
        forward.First().Picks.Select(pick => pick.Work.ExternalId).ShouldBe([20, 30]);
        reversed.Select(row => row.Seed.Title).ShouldBe(["Alpha", "Beta"]);
        reversed.SelectMany(row => row.Picks).Select(pick => pick.Work.ExternalId)
            .ShouldBe(forward.SelectMany(row => row.Picks).Select(pick => pick.Work.ExternalId));
    }

    [Fact]
    public void Rank_ASeedWithNoRecommendations_HasNoRow()
    {
        var empty = Seed(1, 1.0, "Empty");
        var full = Seed(2, 1.0, "Full");
        var recommendations = Recommendations((1, []), (2, [Work(10, 5)]));

        var rows = ForYouRanker.Rank([empty, full], recommendations, NothingExcluded);

        rows.Single().Seed.ShouldBe(full);
    }

    private static RecommendationSeed Seed(int externalId, double weight, string? title = null) =>
        new(new Guid(externalId, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]), externalId, title ?? $"Seed {externalId}", Format.LightNovel, weight);

    private static RecommendedWork Work(int externalId, int votes) => new()
    {
        Source = MetadataSourceName.AniList,
        ExternalId = externalId,
        Title = $"Work {externalId}",
        Format = Format.LightNovel,
        Votes = votes,
    };

    private static Dictionary<int, SeriesRecommendations> Recommendations(params (int ExternalId, RecommendedWork[] Works)[] entries) =>
        entries.ToDictionary(entry => entry.ExternalId, entry => new SeriesRecommendations(entry.ExternalId, entry.Works));
}
