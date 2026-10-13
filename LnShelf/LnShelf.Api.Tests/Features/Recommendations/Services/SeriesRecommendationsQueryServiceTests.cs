using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

public sealed class SeriesRecommendationsQueryServiceTests
{
    [Fact]
    public async Task GetForSeriesAsync_AnotherReadersSeries_IsNullAndAsksNobody()
    {
        var database = NewDatabase();
        var othersSeries = await SeedSeriesAsync(database, TestUsers.SomeoneElse, 1);
        var source = Substitute.For<IRecommendationSource>();

        var response = await CreateService(database, TestUsers.Owner, source)
            .GetForSeriesAsync(othersSeries, CancellationToken.None);

        response.ShouldBeNull();
        await source.DidNotReceiveWithAnyArgs().GetForSeriesAsync(default!, default);
    }

    [Fact]
    public async Task GetForSeriesAsync_NoAniListId_IsEmptyAndAsksNobody()
    {
        var database = NewDatabase();
        var manual = await SeedSeriesAsync(database, TestUsers.Owner, aniListId: null);
        var source = Substitute.For<IRecommendationSource>();

        var response = await CreateService(database, TestUsers.Owner, source)
            .GetForSeriesAsync(manual, CancellationToken.None);

        response.ShouldNotBeNull().Works.ShouldBeEmpty();
        await source.DidNotReceiveWithAnyArgs().GetForSeriesAsync(default!, default);
    }

    [Fact]
    public async Task GetForSeriesAsync_ShelvedAndDismissedWorks_AreLeftOut()
    {
        var database = NewDatabase();
        var seriesId = await SeedSeriesAsync(database, TestUsers.Owner, 1);
        await SeedSeriesAsync(database, TestUsers.Owner, 10);
        await SeedDismissalAsync(database, TestUsers.Owner, 11);
        var source = SourceAnswering(1, Work(10, 9), Work(11, 8), Work(12, 7));

        var response = await CreateService(database, TestUsers.Owner, source)
            .GetForSeriesAsync(seriesId, CancellationToken.None);

        response.ShouldNotBeNull().Works.Select(work => work.AniListId).ShouldBe([12]);
    }

    [Fact]
    public async Task GetForSeriesAsync_AtMostTwelveMostVotedFirst()
    {
        var database = NewDatabase();
        var seriesId = await SeedSeriesAsync(database, TestUsers.Owner, 1);
        // Fifteen works, supplied in ascending vote order so the service has to sort.
        var source = SourceAnswering(1, Enumerable.Range(10, 15).Select(id => Work(id, id)).ToArray());

        var response = await CreateService(database, TestUsers.Owner, source)
            .GetForSeriesAsync(seriesId, CancellationToken.None);

        var works = response.ShouldNotBeNull().Works;
        works.Count.ShouldBe(12);
        works.Select(work => work.Votes).ShouldBe(Enumerable.Range(13, 12).Reverse());
        works.ShouldAllBe(work => work.CoverObjectKey == $"anilist/{work.AniListId}.jpg");
    }

    private static string NewDatabase() => $"series-recommendations-{Guid.CreateVersion7()}";

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    private static SeriesRecommendationsQueryService CreateService(string database, Guid userId, IRecommendationSource source)
    {
        var dbContext = Context(database, userId);
        var covers = Substitute.For<ISeriesCoverCache>();
        covers.EnsureCachedAsync(default, default, default, default)
            .ReturnsForAnyArgs(call => Task.FromResult<string?>($"anilist/{call.ArgAt<long>(1)}.jpg"));
        return new SeriesRecommendationsQueryService(dbContext, source, new RecommendationExclusions(dbContext), covers);
    }

    private static IRecommendationSource SourceAnswering(int seedId, params RecommendedWork[] works)
    {
        var source = Substitute.For<IRecommendationSource>();
        source.GetForSeriesAsync(default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyDictionary<int, SeriesRecommendations>>(
                new Dictionary<int, SeriesRecommendations> { [seedId] = new(seedId, works) }));
        return source;
    }

    private static RecommendedWork Work(int externalId, int votes) => new()
    {
        Source = MetadataSourceName.AniList,
        ExternalId = externalId,
        Title = $"Work {externalId}",
        Format = Format.LightNovel,
        Votes = votes,
    };

    private static async Task<Guid> SeedSeriesAsync(string database, Guid userId, int? aniListId)
    {
        await using var dbContext = Context(database, userId);
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = $"Series {aniListId}",
            Format = Format.LightNovel,
        };
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        return series.Id;
    }

    private static async Task SeedDismissalAsync(string database, Guid userId, int aniListId)
    {
        await using var dbContext = Context(database, userId);
        dbContext.RecommendationDismissals.Add(new RecommendationDismissal
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            Title = $"Work {aniListId}",
            Format = Format.LightNovel,
            DismissedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await dbContext.SaveChangesAsync();
    }
}
