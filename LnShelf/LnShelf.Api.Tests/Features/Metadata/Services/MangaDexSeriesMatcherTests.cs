using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which search result is this series: by its links to AniList and MangaUpdates, never by rank.
/// </summary>
public sealed class MangaDexSeriesMatcherTests
{
    [Fact]
    public void Pick_AniListLink_IsTheRecordForThatId()
    {
        var results = MangaDexFixtures.LoadSearch(MangaDexFixtures.ChainsawManSearch).Data!;

        MangaDexSeriesMatcher.Pick(results, MangaDexFixtures.ChainsawManAniListId, null)!.Id
            .ShouldBe(MangaDexFixtures.ChainsawManId);
    }

    // The top hit for "Slime" is the Tensura Nikki spin-off. It is never the answer just for
    // ranking first.
    [Fact]
    public void Pick_SpinOffRankedFirst_IsPassedOverForTheLinkedSeries()
    {
        var results = MangaDexFixtures.LoadSearch(MangaDexFixtures.SlimeSearch).Data!;

        results[0].Id.ShouldNotBe(MangaDexFixtures.SlimeId);
        MangaDexSeriesMatcher.Pick(results, MangaDexFixtures.SlimeAniListId, null)!.Id.ShouldBe(MangaDexFixtures.SlimeId);
    }

    // A series added from MangaUpdates has no AniList id. Slime's slug is "04gtb06" — zero-padded.
    [Fact]
    public void Pick_OnlyAMangaUpdatesId_MatchesItsZeroPaddedBase36Slug()
    {
        var results = MangaDexFixtures.LoadSearch(MangaDexFixtures.SlimeSearch).Data!;

        MangaDexSeriesMatcher.Pick(results, null, MangaDexFixtures.SlimeMangaUpdatesId)!.Id
            .ShouldBe(MangaDexFixtures.SlimeId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    public void Pick_NoResultLinkedToTheSeries_IsNothing(int? aniListId)
    {
        var results = MangaDexFixtures.LoadSearch(MangaDexFixtures.SlimeSearch).Data!;

        MangaDexSeriesMatcher.Pick(results, aniListId, 42).ShouldBeNull();
    }

    [Theory]
    [InlineData(75336092483, "ylx5wzn")]
    [InlineData(270105846, "4gtb06")]
    [InlineData(15180124327, "6z1uqw7")]
    [InlineData(0, "0")]
    public void ToMangaUpdatesSlug_AnId_IsItInLowerCaseBase36(long seriesId, string expected) =>
        MangaDexSeriesMatcher.ToMangaUpdatesSlug(seriesId).ShouldBe(expected);
}
