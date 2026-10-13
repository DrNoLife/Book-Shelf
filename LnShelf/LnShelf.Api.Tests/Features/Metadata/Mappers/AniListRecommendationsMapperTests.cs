using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// What readers also liked, out of a captured <c>Recommendations</c> response (testing.md #4),
/// plus the filters the capture happens not to exercise, built in code.
/// </summary>
public sealed class AniListRecommendationsMapperTests
{
    private static AniListMedia Captured(int id) =>
        AniListFixtures.LoadRecommendations(AniListFixtures.MushokuTenseiRecommendations)
            .Page!.Media!.Single(media => media.Id == id);

    [Fact]
    public void ToRecommendations_TheCapturedNovel_KeepsOnlyLightNovelsMostVotedFirst()
    {
        var recommendations = AniListRecommendationsMapper.ToRecommendations(
            Captured(AniListFixtures.MushokuTenseiNovelId));

        recommendations.ExternalId.ShouldBe(AniListFixtures.MushokuTenseiNovelId);
        recommendations.Works.ShouldAllBe(work => work.Format == Format.LightNovel);
        recommendations.Works.ShouldAllBe(work => work.Source == MetadataSourceName.AniList);

        // As captured on 2026-10-13: ten novels, Re:ZERO first at 124 votes. A fresh capture
        // may reorder the tail, so only the head and the ordering are asserted.
        recommendations.Works.Count.ShouldBe(10);
        recommendations.Works[0].ExternalId.ShouldBe(85737);
        recommendations.Works[0].Votes.ShouldBe(124);
        recommendations.Works.Select(work => work.Votes).ShouldBe(
            recommendations.Works.Select(work => work.Votes).OrderDescending());
    }

    [Fact]
    public void ToRecommendations_TheCapturedManga_KeepsOnlyManga()
    {
        var recommendations = AniListRecommendationsMapper.ToRecommendations(
            Captured(AniListFixtures.MushokuTenseiMangaId));

        recommendations.Works.ShouldNotBeEmpty();
        recommendations.Works.ShouldAllBe(work => work.Format == Format.Manga);
        recommendations.Works[0].ExternalId.ShouldBe(86399);
    }

    [Fact]
    public void ToRecommendations_TheCapturedNovel_LeavesItselfOut()
    {
        var recommendations = AniListRecommendationsMapper.ToRecommendations(
            Captured(AniListFixtures.MushokuTenseiNovelId));

        recommendations.Works.ShouldNotContain(work => work.ExternalId == AniListFixtures.MushokuTenseiNovelId);
    }

    [Fact]
    public void ToRecommendations_FilteredNodes_AreSkipped()
    {
        var series = Seed(
            "MANGA",
            Node(10, rating: 5, format: "MANGA", title: "Kept"),
            Node(11, rating: 5, format: "MANGA", title: "Adult", isAdult: true),
            Node(12, rating: 1, format: "MANGA", title: "One vote"),
            Node(13, rating: null, format: "MANGA", title: "No votes"),
            new AniListRecommendation { Rating = 9, MediaRecommendation = null },
            Node(14, rating: 9, format: "NOVEL", title: "Other format"),
            Node(15, rating: 4, format: "ONE_SHOT", title: "One shot is manga"),
            Node(1, rating: 9, format: "MANGA", title: "Itself"),
            Node(10, rating: 8, format: "MANGA", title: "Kept, twice"),
            Node(16, rating: 9, format: "MANGA", title: null));

        var works = AniListRecommendationsMapper.ToRecommendations(series).Works;

        works.Select(work => work.ExternalId).ShouldBe(new[] { 10, 15 });
        works[0].Title.ShouldBe("Kept");
        works[0].Votes.ShouldBe(5);
        works[1].Format.ShouldBe(Format.Manga);
    }

    [Fact]
    public void ToRecommendations_EqualVotes_AreOrderedByExternalId()
    {
        var series = Seed(
            "NOVEL",
            Node(30, rating: 4, format: "NOVEL", title: "B"),
            Node(20, rating: 4, format: "NOVEL", title: "A"),
            Node(40, rating: 7, format: "NOVEL", title: "C"));

        AniListRecommendationsMapper.ToRecommendations(series).Works
            .Select(work => work.ExternalId).ShouldBe(new[] { 40, 20, 30 });
    }

    [Fact]
    public void ToRecommendations_NoEnglishTitle_FallsBackToRomaji()
    {
        var series = Seed("NOVEL", new AniListRecommendation
        {
            Rating = 3,
            MediaRecommendation = new AniListMedia
            {
                Id = 10,
                Format = "NOVEL",
                Title = new AniListMediaTitle { English = " ", Romaji = "Romaji", Native = "Native" },
            },
        });

        AniListRecommendationsMapper.ToRecommendations(series).Works.ShouldHaveSingleItem()
            .Title.ShouldBe("Romaji");
    }

    [Fact]
    public void ToRecommendations_AnUntrackedSeedFormat_IsNone()
    {
        var series = Seed("NOVEL_ARTBOOK", Node(10, rating: 5, format: "NOVEL_ARTBOOK", title: "Anything"));

        AniListRecommendationsMapper.ToRecommendations(series).ShouldBe(SeriesRecommendations.None(1));
    }

    private static AniListMedia Seed(string format, params AniListRecommendation[] nodes) => new()
    {
        Id = 1,
        Format = format,
        Recommendations = new AniListRecommendationConnection { Nodes = [.. nodes] },
    };

    private static AniListRecommendation Node(
        int id, int? rating, string format, string? title, bool? isAdult = false) => new()
        {
            Rating = rating,
            MediaRecommendation = new AniListMedia
            {
                Id = id,
                Format = format,
                IsAdult = isAdult,
                Title = title is null ? null : new AniListMediaTitle { English = title },
            },
        };
}
