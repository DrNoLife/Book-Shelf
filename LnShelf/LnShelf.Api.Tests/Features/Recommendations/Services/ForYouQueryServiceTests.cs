using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

public sealed class ForYouQueryServiceTests
{
    [Fact]
    public async Task GetAsync_NothingRead_AsksNobodyAndHasNoSeeds()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.PlanToStart);
        await SeedSeriesAsync(database, TestUsers.Owner, 2, TrackingStatus.Reading, readVolumes: 0);
        var source = Substitute.For<IRecommendationSource>();

        var response = await CreateService(database, TestUsers.Owner, source).GetAsync(CancellationToken.None);

        response.SeedCount.ShouldBe(0);
        response.Rows.ShouldBeEmpty();
        await source.DidNotReceiveWithAnyArgs().GetForSeriesAsync(default!, default);
    }

    [Fact]
    public async Task GetAsync_ACompletedAndAReadingSeries_AsksOnceForBothIds()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.Completed, readVolumes: 3);
        await SeedSeriesAsync(database, TestUsers.Owner, 2, TrackingStatus.Reading, readVolumes: 1);
        var source = SourceAnswering((1, [Work(10, 9)]), (2, [Work(11, 5)]));

        var response = await CreateService(database, TestUsers.Owner, source).GetAsync(CancellationToken.None);

        await source.Received(1).GetForSeriesAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 1, 2 })),
            Arg.Any<CancellationToken>());
        response.SeedCount.ShouldBe(2);
        response.Rows.SelectMany(row => row.Works).Select(work => work.AniListId).Order().ShouldBe([10, 11]);
    }

    [Fact]
    public async Task GetAsync_RemovesWhatIsOnTheShelfAndWhatWasDismissed()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.Completed, readVolumes: 3);
        await SeedSeriesAsync(database, TestUsers.Owner, 10, TrackingStatus.PlanToStart);
        await SeedDismissalAsync(database, TestUsers.Owner, 11);
        var source = SourceAnswering((1, [Work(10, 9), Work(11, 8), Work(12, 7)]));

        var response = await CreateService(database, TestUsers.Owner, source).GetAsync(CancellationToken.None);

        response.Rows.Single().Works.Select(work => work.AniListId).ShouldBe([12]);
    }

    [Fact]
    public async Task GetAsync_AnotherUsersShelfAndDismissals_DoNotCount()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.Completed, readVolumes: 3);
        await SeedSeriesAsync(database, TestUsers.SomeoneElse, 2, TrackingStatus.Completed, readVolumes: 3);
        await SeedSeriesAsync(database, TestUsers.SomeoneElse, 10, TrackingStatus.Completed, readVolumes: 3);
        await SeedDismissalAsync(database, TestUsers.SomeoneElse, 11);
        var source = SourceAnswering((1, [Work(10, 9), Work(11, 8)]));

        var response = await CreateService(database, TestUsers.Owner, source).GetAsync(CancellationToken.None);

        // Their series 2 is no seed; their series 10 and dismissal of 11 hide nothing here.
        await source.Received(1).GetForSeriesAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1 })),
            Arg.Any<CancellationToken>());
        response.SeedCount.ShouldBe(1);
        response.Rows.Single().Works.Select(work => work.AniListId).ShouldBe([10, 11]);
    }

    [Fact]
    public async Task GetAsync_CoversAreCachedOnlyForShownPicks()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.Completed, readVolumes: 3);
        // Twelve suggestions, eight shown.
        var source = SourceAnswering((1, Enumerable.Range(10, 12).Select(id => Work(id, 100 - id)).ToArray()));
        var covers = CoverCache();

        var response = await CreateService(database, TestUsers.Owner, source, covers).GetAsync(CancellationToken.None);

        var works = response.Rows.Single().Works;
        works.Count.ShouldBe(8);
        works.ShouldAllBe(work => work.CoverObjectKey == $"anilist/{work.AniListId}.jpg");
        await covers.Received(8).EnsureCachedAsync(
            Arg.Any<MetadataSourceName>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_SourceUnavailable_Throws()
    {
        var database = NewDatabase();
        await SeedSeriesAsync(database, TestUsers.Owner, 1, TrackingStatus.Completed, readVolumes: 3);
        var source = Substitute.For<IRecommendationSource>();
        source.GetForSeriesAsync(default!, default)
            .ReturnsForAnyArgs<Task<IReadOnlyDictionary<int, SeriesRecommendations>>>(
                _ => throw new MetadataSourceUnavailableException("AniList", "down"));

        var act = () => CreateService(database, TestUsers.Owner, source).GetAsync(CancellationToken.None);

        await Should.ThrowAsync<MetadataSourceUnavailableException>(act);
    }

    private static string NewDatabase() => $"for-you-{Guid.CreateVersion7()}";

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    private static ForYouQueryService CreateService(
        string database,
        Guid userId,
        IRecommendationSource source,
        ISeriesCoverCache? covers = null)
    {
        var dbContext = Context(database, userId);
        return new ForYouQueryService(
            dbContext,
            source,
            new RecommendationExclusions(dbContext),
            covers ?? CoverCache(),
            new FixedTimeProvider(),
            TestUsers.As(userId),
            NullLogger<ForYouQueryService>.Instance);
    }

    private static ISeriesCoverCache CoverCache()
    {
        var covers = Substitute.For<ISeriesCoverCache>();
        covers.EnsureCachedAsync(default, default, default, default)
            .ReturnsForAnyArgs(call => Task.FromResult<string?>($"anilist/{call.ArgAt<long>(1)}.jpg"));
        return covers;
    }

    private static IRecommendationSource SourceAnswering(params (int ExternalId, RecommendedWork[] Works)[] entries)
    {
        var answer = entries.ToDictionary(
            entry => entry.ExternalId,
            entry => new SeriesRecommendations(entry.ExternalId, entry.Works));
        var source = Substitute.For<IRecommendationSource>();
        source.GetForSeriesAsync(default!, default)
            .ReturnsForAnyArgs(call => Task.FromResult<IReadOnlyDictionary<int, SeriesRecommendations>>(
                call.Arg<IReadOnlyCollection<int>>().ToDictionary(
                    id => id,
                    id => answer.GetValueOrDefault(id) ?? SeriesRecommendations.None(id))));
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

    private static async Task SeedSeriesAsync(
        string database, Guid userId, int aniListId, TrackingStatus status, int readVolumes = 0)
    {
        await using var dbContext = Context(database, userId);
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = $"Series {aniListId}",
            Format = Format.LightNovel,
            UserState = new SeriesUserState { TrackingStatus = status, LastReadAt = new DateOnly(2026, 10, 1) },
        };
        for (var number = 1; number <= readVolumes; number++)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                UserState = new VolumeUserState { ReadStatus = ReadStatus.Read },
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
