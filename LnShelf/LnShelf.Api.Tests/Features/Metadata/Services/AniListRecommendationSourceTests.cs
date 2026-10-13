using System.Net;
using System.Text.Json;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The batched, shared recommendation cache against the captured AniList answer. The cache
/// tests double as proof that a get-only HybridCache read (DisableUnderlyingData) runs no
/// factory and writes nothing, which the batch path is built on.
/// </summary>
public sealed class AniListRecommendationSourceTests
{
    private const int Novel = AniListFixtures.MushokuTenseiNovelId;
    private const int Manga = AniListFixtures.MushokuTenseiMangaId;

    [Fact]
    public async Task GetForSeriesAsync_TwoIds_AsksOnceAndAnswersBoth()
    {
        using var upstream = new CountingHandler();

        var result = await CreateSource(upstream, CreateCache())
            .GetForSeriesAsync([Novel, Manga], CancellationToken.None);

        upstream.RequestedIds.Count.ShouldBe(1);
        upstream.RequestedIds[0].ShouldBe([Novel, Manga]);
        result.Keys.ShouldBe([Novel, Manga], ignoreOrder: true);
        result[Novel].Works.ShouldNotBeEmpty();
        result[Manga].Works.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetForSeriesAsync_AskedAgain_AnswersFromTheCacheWithNoRequest()
    {
        using var upstream = new CountingHandler();
        var source = CreateSource(upstream, CreateCache());

        var first = await source.GetForSeriesAsync([Novel, Manga], CancellationToken.None);
        var second = await source.GetForSeriesAsync([Novel, Manga], CancellationToken.None);

        upstream.RequestedIds.Count.ShouldBe(1);
        second[Novel].Works.Select(work => work.ExternalId).ShouldBe(first[Novel].Works.Select(work => work.ExternalId));
        second[Manga].Works.Count.ShouldBe(first[Manga].Works.Count);
    }

    [Fact]
    public async Task GetForSeriesAsync_OneCachedOneNew_AsksOnlyForTheNewId()
    {
        using var upstream = new CountingHandler();
        var source = CreateSource(upstream, CreateCache());

        await source.GetForSeriesAsync([Novel], CancellationToken.None);
        var result = await source.GetForSeriesAsync([Novel, Manga], CancellationToken.None);

        upstream.RequestedIds.Count.ShouldBe(2);
        upstream.RequestedIds[1].ShouldBe([Manga]);
        result[Novel].Works.ShouldNotBeEmpty();
        result[Manga].Works.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetForSeriesAsync_AnIdAniListDoesNotKnow_IsEmptyAndNotAskedAgain()
    {
        using var upstream = new CountingHandler();
        var source = CreateSource(upstream, CreateCache());

        var first = await source.GetForSeriesAsync([Novel, 999999], CancellationToken.None);
        var second = await source.GetForSeriesAsync([Novel, 999999], CancellationToken.None);

        first[999999].Works.ShouldBeEmpty();
        second[999999].Works.ShouldBeEmpty();
        upstream.RequestedIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetForSeriesAsync_SixtyIds_AsksInTwoRequestsOfFiftyAndTen()
    {
        using var upstream = new CountingHandler();
        var ids = Enumerable.Range(1000, 60).ToList();

        var result = await CreateSource(upstream, CreateCache())
            .GetForSeriesAsync(ids, CancellationToken.None);

        upstream.RequestedIds.Select(requested => requested.Length).ShouldBe(new[] { 50, 10 });
        result.Count.ShouldBe(60);
    }

    [Fact]
    public async Task GetForSeriesAsync_AniListRefuses_ThrowsAndCachesNothing()
    {
        using var upstream = new CountingHandler { RefuseNext = true };
        var source = CreateSource(upstream, CreateCache());

        await Should.ThrowAsync<MetadataSourceUnavailableException>(
            () => source.GetForSeriesAsync([Novel], CancellationToken.None));

        var result = await source.GetForSeriesAsync([Novel], CancellationToken.None);

        upstream.RequestedIds.Count.ShouldBe(2);
        result[Novel].Works.ShouldNotBeEmpty();
    }

    /// <summary>
    /// The get-only read must neither run a factory nor store anything: a miss returns null
    /// and the next read still misses.
    /// </summary>
    [Fact]
    public async Task HybridCache_GetOnlyRead_RunsNoFactoryAndWritesNothing()
    {
        var cache = CreateCache();
        var factoryRuns = 0;
        var options = new HybridCacheEntryOptions
        {
            Flags = HybridCacheEntryFlags.DisableUnderlyingData
                    | HybridCacheEntryFlags.DisableLocalCacheWrite
                    | HybridCacheEntryFlags.DisableDistributedCache,
        };

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var value = await cache.GetOrCreateAsync<string?>(
                "get-only-probe",
                _ =>
                {
                    factoryRuns++;
                    return ValueTask.FromResult<string?>("from the factory");
                },
                options,
                cancellationToken: CancellationToken.None);

            value.ShouldBeNull();
        }

        factoryRuns.ShouldBe(0);

        await cache.SetAsync("get-only-probe", "stored", cancellationToken: CancellationToken.None);

        (await cache.GetOrCreateAsync<string?>(
            "get-only-probe",
            static _ => ValueTask.FromResult<string?>(null),
            options,
            cancellationToken: CancellationToken.None)).ShouldBe("stored");
    }

    private static AniListRecommendationSource CreateSource(HttpMessageHandler upstream, HybridCache cache)
    {
        var httpClient = new HttpClient(upstream, disposeHandler: false)
        {
            BaseAddress = new AniListOptions().Endpoint,
        };

        return new AniListRecommendationSource(
            new AniListGraphQlClient(httpClient, NullLogger<AniListGraphQlClient>.Instance),
            cache,
            NullLogger<AniListRecommendationSource>.Instance);
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    /// <summary>
    /// Answers the captured Recommendations response for the ids it holds and an empty Page
    /// for the rest, and records the ids each request asked for.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly AniListSearchData _captured = AniListFixtures.LoadRecommendations(
            AniListFixtures.MushokuTenseiRecommendations);

        public List<int[]> RequestedIds { get; } = [];

        /// <summary>When set, the next request is refused with a 503 and an errors envelope.</summary>
        public bool RefuseNext { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            var ids = document.RootElement.GetProperty("variables").GetProperty("ids")
                .EnumerateArray().Select(element => element.GetInt32()).ToArray();

            RequestedIds.Add(ids);

            if (RefuseNext)
            {
                RefuseNext = false;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(
                        """{"errors":[{"message":"Service unavailable","status":503}],"data":null}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            var media = _captured.Page!.Media!.Where(candidate => ids.Contains(candidate.Id)).ToList();
            var envelope = new AniListGraphQlResponse<AniListSearchData>
            {
                Data = new AniListSearchData { Page = new AniListPage { Media = media } },
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(envelope, AniListGraphQlClient.JsonOptions),
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
