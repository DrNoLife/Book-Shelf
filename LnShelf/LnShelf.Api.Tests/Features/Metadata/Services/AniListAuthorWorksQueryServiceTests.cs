using System.Net;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// "More by {author}" against the captured AniList answer: one request per series a day,
/// and "on your shelf" decided per reader, never cached with AniList's data.
/// </summary>
public sealed class AniListAuthorWorksQueryServiceTests
{
    [Fact]
    public async Task GetForSeriesAsync_AWorkOnTheShelf_IsMarkedWithTheReadersOwnSeries()
    {
        using var upstream = new CountingHandler();
        var databaseName = NewDatabaseName();
        await using var dbContext = CreateDbContext(databaseName, TestUsers.Owner);
        var novel = await SeedAsync(dbContext, AniListFixtures.MushokuTenseiNovelId, Format.LightNovel);
        var manga = await SeedAsync(dbContext, AniListFixtures.MushokuTenseiMangaId, Format.Manga);

        var response = await CreateService(dbContext, upstream, CreateCache())
            .GetForSeriesAsync(novel.Id, CancellationToken.None);

        response.ShouldNotBeNull();
        response.Author.ShouldBe("Rifujin na Magonote");
        response.Works.Single(work => work.AniListId == AniListFixtures.MushokuTenseiMangaId)
            .ShelfSeriesId.ShouldBe(manga.Id);
        response.Works.Where(work => work.AniListId != AniListFixtures.MushokuTenseiMangaId)
            .ShouldAllBe(work => work.ShelfSeriesId == null);
    }

    /// <summary>
    /// The cache holds AniList's answer for everyone, so the marks have to be worked out
    /// after it, against whoever is asking.
    /// </summary>
    [Fact]
    public async Task GetForSeriesAsync_TwoReadersOfTheSameSeries_ShareOneRequestButNotTheMarks()
    {
        using var upstream = new CountingHandler();
        var cache = CreateCache();
        var databaseName = NewDatabaseName();

        await using var ownerContext = CreateDbContext(databaseName, TestUsers.Owner);
        var ownersNovel = await SeedAsync(ownerContext, AniListFixtures.MushokuTenseiNovelId, Format.LightNovel);
        await SeedAsync(ownerContext, AniListFixtures.MushokuTenseiMangaId, Format.Manga);

        await using var otherContext = CreateDbContext(databaseName, TestUsers.SomeoneElse);
        var othersNovel = await SeedAsync(otherContext, AniListFixtures.MushokuTenseiNovelId, Format.LightNovel);

        var owners = await CreateService(ownerContext, upstream, cache)
            .GetForSeriesAsync(ownersNovel.Id, CancellationToken.None);
        var others = await CreateService(otherContext, upstream, cache)
            .GetForSeriesAsync(othersNovel.Id, CancellationToken.None);

        upstream.Calls.ShouldBe(1);
        owners.ShouldNotBeNull().Works.ShouldContain(work => work.ShelfSeriesId != null);

        // The second answer came out of the cache: it must carry AniList's data, not an empty
        // list that would pass the "no marks" check below for the wrong reason.
        others.ShouldNotBeNull();
        others.Author.ShouldBe(owners.Author);
        others.Works.ShouldNotBeEmpty();
        others.Works.Count.ShouldBe(owners.Works.Count);
        others.Works.ShouldAllBe(work => work.ShelfSeriesId == null);
    }

    [Fact]
    public async Task GetForSeriesAsync_ASeriesWithNoAniListId_AsksNobody()
    {
        using var upstream = new CountingHandler();
        await using var dbContext = CreateDbContext(NewDatabaseName(), TestUsers.Owner);
        var manual = await SeedAsync(dbContext, aniListId: null, Format.WebNovel);

        var response = await CreateService(dbContext, upstream, CreateCache())
            .GetForSeriesAsync(manual.Id, CancellationToken.None);

        response.ShouldNotBeNull().Works.ShouldBeEmpty();
        response.Author.ShouldBe("Someone");
        upstream.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task GetForSeriesAsync_AnotherReadersSeries_IsNotFound()
    {
        using var upstream = new CountingHandler();
        var databaseName = NewDatabaseName();

        await using var otherContext = CreateDbContext(databaseName, TestUsers.SomeoneElse);
        var othersNovel = await SeedAsync(otherContext, AniListFixtures.MushokuTenseiNovelId, Format.LightNovel);

        await using var ownerContext = CreateDbContext(databaseName, TestUsers.Owner);

        var response = await CreateService(ownerContext, upstream, CreateCache())
            .GetForSeriesAsync(othersNovel.Id, CancellationToken.None);

        response.ShouldBeNull();
        upstream.Calls.ShouldBe(0);
    }

    private static string NewDatabaseName() => $"author-works-{Guid.CreateVersion7()}";

    private static async Task<Series> SeedAsync(LnShelfDbContext dbContext, int? aniListId, Format format)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
            Format = format,
            Author = "Someone",
        };

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        return series;
    }

    private static AniListAuthorWorksQueryService CreateService(
        LnShelfDbContext dbContext,
        HttpMessageHandler upstream,
        HybridCache cache)
    {
        var httpClient = new HttpClient(upstream, disposeHandler: false)
        {
            BaseAddress = new AniListOptions().Endpoint,
        };

        var coverCache = Substitute.For<ISeriesCoverCache>();
        coverCache
            .EnsureCachedAsync(Arg.Any<MetadataSourceName>(), Arg.Any<long>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => $"anilist/{call.ArgAt<long>(1)}.jpg");

        return new AniListAuthorWorksQueryService(
            dbContext,
            new AniListGraphQlClient(httpClient, NullLogger<AniListGraphQlClient>.Instance),
            coverCache,
            cache,
            NullLogger<AniListAuthorWorksQueryService>.Instance);
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static LnShelfDbContext CreateDbContext(string databaseName, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, TestUsers.As(userId));

    /// <summary>Answers the captured AuthorWorks response, and counts how often it was asked.</summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly string _json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", AniListFixtures.MushokuTenseiNovelAuthorWorks));

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
