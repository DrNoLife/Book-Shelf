using System.Net;
using System.Text.Json;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// One <c>series/search</c> request behind both readers of it: the release source's link
/// search and the add flow's search-and-add both ask MangaUpdates the same question.
/// </summary>
// There used to be two cache keys over the identical POST, so looking a series up and then
// adding it made the same request twice against a free community API (CLAUDE.md — be a polite
// API citizen).
public sealed class MangaUpdatesSearchCacheTests
{
    [Fact]
    public async Task SearchAsync_BothCallersAskingTheSameQuestion_ReachesTheSourceOnce()
    {
        using var upstream = new CountingHandler(MangaUpdatesFixtures.YoujoSenkiSearch);
        var (releaseSource, queryService) = CreateServices(upstream);

        var candidates = await releaseSource.SearchAsync(
            "Youjo Senki", Format.LightNovel, CancellationToken.None);
        var results = await queryService.SearchAsync(
            "Youjo Senki", Format.LightNovel, CancellationToken.None);

        // The whole point: one upstream request, two clean shapes mapped out of it.
        upstream.Calls.ShouldBe(1);
        candidates.ShouldNotBeEmpty();
        results.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TheSameCallerTwice_StillReachesTheSourceOnce()
    {
        using var upstream = new CountingHandler(MangaUpdatesFixtures.YoujoSenkiSearch);
        var (releaseSource, _) = CreateServices(upstream);

        await releaseSource.SearchAsync("Youjo Senki", Format.LightNovel, CancellationToken.None);
        await releaseSource.SearchAsync("youjo senki", Format.LightNovel, CancellationToken.None);

        // Case is folded into the key, so a differently-typed search of the same title is the
        // same question.
        upstream.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task SearchAsync_ADifferentFormat_IsADifferentQuestion()
    {
        using var upstream = new CountingHandler(MangaUpdatesFixtures.YoujoSenkiSearch);
        var (releaseSource, _) = CreateServices(upstream);

        await releaseSource.SearchAsync("Youjo Senki", Format.LightNovel, CancellationToken.None);
        await releaseSource.SearchAsync("Youjo Senki", Format.Manga, CancellationToken.None);

        // The format is part of the request, so sharing a key across formats would serve a
        // manga search the novel's answer.
        upstream.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SearchAsync_TheSharedRequest_AsksForTheLongerOfTheTwoLists()
    {
        using var upstream = new CountingHandler(MangaUpdatesFixtures.YoujoSenkiSearch);
        var (releaseSource, _) = CreateServices(upstream);

        await releaseSource.SearchAsync("Youjo Senki", Format.LightNovel, CancellationToken.None);

        // The person's search wants twenty and the matcher five; one request serves both only
        // if it asks for twenty. It used to ask for the matcher's five, which was all a person
        // ever saw when MangaUpdates answered the search (audit 2026-10-09, #11).
        upstream.RequestedPageSizes.ShouldBe([20]);
    }

    [Fact]
    public async Task SearchAsync_EachReader_TakesOnlyItsOwnNumberOfHits()
    {
        using var upstream = new CountingHandler(MangaUpdatesFixtures.YoujoSenkiSearch);
        var (releaseSource, queryService) = CreateServices(
            upstream,
            new MangaUpdatesOptions { SearchResultLimit = 2, FallbackSearchResultLimit = 4 });

        var candidates = await releaseSource.SearchAsync(
            "Youjo Senki", Format.LightNovel, CancellationToken.None);
        var results = await queryService.SearchAsync(
            "Youjo Senki", Format.LightNovel, CancellationToken.None);

        // Still one request, and each reader its own prefix of it: the matcher fetches every
        // candidate it is handed, so a longer shared page must not become a longer fetch list.
        upstream.Calls.ShouldBe(1);
        upstream.RequestedPageSizes.ShouldBe([4]);
        candidates.Count.ShouldBe(2);
        results.Count.ShouldBe(4);

        // A prefix, in the source's own order: the matcher's best guess is still the first hit.
        candidates[0].ExternalId.ShouldBe(results[0].SourceId);
    }

    private static (MangaUpdatesReleaseSource ReleaseSource, MangaUpdatesSeriesQueryService QueryService)
        CreateServices(HttpMessageHandler upstream, MangaUpdatesOptions? settings = null)
    {
        var options = Options.Create(settings ?? new MangaUpdatesOptions());

        var httpClient = new HttpClient(upstream, disposeHandler: false)
        {
            BaseAddress = options.Value.Endpoint,
        };

        var client = new MangaUpdatesClient(httpClient, NullLogger<MangaUpdatesClient>.Instance);

        // A real HybridCache rather than a fake: what is under test is that two callers share
        // one entry, which is a fact about the cache and not about a substitute.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();

        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        var searchCache = new MangaUpdatesSearchCache(
            client, cache, options, NullLogger<MangaUpdatesSearchCache>.Instance);

        return (
            new MangaUpdatesReleaseSource(
                client, searchCache, options, NullLogger<MangaUpdatesReleaseSource>.Instance),
            new MangaUpdatesSeriesQueryService(
                client, searchCache, options, NullLogger<MangaUpdatesSeriesQueryService>.Instance));
    }

    /// <summary>Answers one captured search response, and counts how often it was asked.</summary>
    private sealed class CountingHandler(string fixtureFileName) : HttpMessageHandler
    {
        private readonly string _json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fixtureFileName));

        public int Calls { get; private set; }

        /// <summary>The page size each search asked MangaUpdates for.</summary>
        public List<int> RequestedPageSizes { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            RequestedPageSizes.Add(document.RootElement.GetProperty("perpage").GetInt32());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
