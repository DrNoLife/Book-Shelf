using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Adding from AniList records AniList as the count's basis exactly when the release source
/// gave no count; otherwise the refresh after the add writes the release source's (D11).
/// </summary>
public sealed class AddTimeReleaseLinkTests
{
    [Fact]
    public async Task ApplyAsync_NoLink_RecordsAniListAsTheBasis()
    {
        var series = await ApplyAsync(Matcher(new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.NoCandidate,
            Explanation = "Nothing found.",
        }));

        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.AniList);
        series.VolumeCountEvidence.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_LinkedWithoutACount_RecordsAniListAsTheBasis()
    {
        var series = await ApplyAsync(Matcher(Linked(volumeCount: null)));

        series.MangaUpdatesId.ShouldBe(SourceId);
        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.AniList);
    }

    [Fact]
    public async Task ApplyAsync_TheLookupFails_RecordsAniListAsTheBasis()
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns<ReleaseSourceMatch>(_ => throw new HttpRequestException("down"));

        var series = await ApplyAsync(matcher);

        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.AniList);
    }

    [Fact]
    public async Task ApplyAsync_TheReleaseSourceCounts_LeavesTheBasisToTheRefresh()
    {
        var series = await ApplyAsync(Matcher(Linked(volumeCount: 9)));

        series.Volumes.Count.ShouldBe(9);
        series.VolumeCountBasis.ShouldBeNull();
    }

    private const long SourceId = 17992364412;

    private static ReleaseSourceMatch Linked(int? volumeCount) => new()
    {
        Outcome = ReleaseMatchOutcome.Linked,
        MatchedExternalId = SourceId,
        MatchedSeries = new ReleaseSeriesInfo { ExternalId = SourceId, Title = "Fremd Torturchen", VolumeCount = volumeCount },
        Explanation = "Linked.",
    };

    private static IReleaseSourceMatcher Matcher(ReleaseSourceMatch match)
    {
        var matcher = Substitute.For<IReleaseSourceMatcher>();
        matcher.MatchAsync(Arg.Any<Series>(), Arg.Any<IReadOnlySet<long>>(), Arg.Any<CancellationToken>())
            .Returns(match);

        return matcher;
    }

    private static async Task<Series> ApplyAsync(IReleaseSourceMatcher matcher)
    {
        await using var dbContext = new LnShelfDbContext(
            new DbContextOptionsBuilder<LnShelfDbContext>()
                .UseInMemoryDatabase($"add-time-link-{Guid.CreateVersion7()}")
                .Options,
            TestUsers.Current);

        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 101,
            TitleEnglish = "Torture Princess",
            Format = Format.LightNovel,
        };

        foreach (var number in Enumerable.Range(1, 10))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        await new AddTimeReleaseLink(matcher, dbContext, NullLogger<AddTimeReleaseLink>.Instance)
            .ApplyAsync(series, CancellationToken.None);

        return series;
    }
}
