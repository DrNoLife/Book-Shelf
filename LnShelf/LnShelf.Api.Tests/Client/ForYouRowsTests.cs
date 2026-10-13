using LnShelf.Client.Features.Recommendations;
using LnShelf.Shared.Recommendations;

namespace LnShelf.Api.Tests.Client;

/// <summary>Not interested edits the loaded page in place: the work leaves every row.</summary>
public sealed class ForYouRowsTests
{
    private static RecommendationResponse Work(int aniListId) => new()
    {
        AniListId = aniListId,
        TitleEnglish = $"Work {aniListId}",
        Format = Format.LightNovel,
        Votes = 5,
    };

    private static ForYouRowResponse Row(params int[] aniListIds) => new()
    {
        BecauseOfSeriesId = Guid.NewGuid(),
        BecauseOfTitle = "Seed",
        Format = Format.LightNovel,
        Works = [.. aniListIds.Select(Work)],
    };

    [Fact]
    public void Without_AWorkInOneRow_RemovesOnlyIt()
    {
        var response = new ForYouResponse { SeedCount = 2, Rows = [Row(1, 2), Row(3)] };

        var result = ForYouRows.Without(response, 1);

        Assert.Equal([2], result.Rows[0].Works.Select(work => work.AniListId));
        Assert.Equal([3], result.Rows[1].Works.Select(work => work.AniListId));
    }

    [Fact]
    public void Without_TheLastWorkOfARow_DropsTheRow()
    {
        var response = new ForYouResponse { SeedCount = 2, Rows = [Row(1), Row(2, 3)] };

        var result = ForYouRows.Without(response, 1);

        var remaining = Assert.Single(result.Rows);
        Assert.Equal([2, 3], remaining.Works.Select(work => work.AniListId));
    }

    [Fact]
    public void Without_AnUnknownId_ChangesNothing()
    {
        var response = new ForYouResponse { SeedCount = 1, Rows = [Row(1, 2)] };

        var result = ForYouRows.Without(response, 99);

        Assert.Equal([1, 2], result.Rows.Single().Works.Select(work => work.AniListId));
    }

    [Fact]
    public void Without_KeepsSeedCount()
    {
        var response = new ForYouResponse { SeedCount = 7, Rows = [Row(1)] };

        Assert.Equal(7, ForYouRows.Without(response, 1).SeedCount);
    }
}
