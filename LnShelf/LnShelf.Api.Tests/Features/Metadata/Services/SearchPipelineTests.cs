using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The series search as the app wires it — the real search service, AniList and MangaUpdates
/// clients, rate-limit handlers and shared search cache — with only the bytes on the wire faked.
/// </summary>
// The unit tests beside this each construct one class with `new`. What they cannot see is the
// two things this file is about, both of which are facts of the wiring: which page size the one
// shared MangaUpdates request asks for when it answers in AniList's place (audit 2026-10-09,
// #11), and what a person searching is told when AniList is throttling the app (#20).
public sealed class SearchPipelineTests
{
    private const string Query = "Youjo Senki";

    [Fact]
    public async Task SearchAsync_AniListDisabled_ShowsTheFallbacksFullListWhileTheMatcherKeepsItsOwn()
    {
        using var host = new SearchHost(
            HttpStatusCode.Forbidden,
            new Dictionary<string, string?> { ["MangaUpdates:SearchResultLimit"] = "2" });

        var outcome = await host.SearchAsync();
        var candidates = await host.LinkCandidatesAsync();

        outcome.Source.ShouldBe(MetadataSourceName.MangaUpdates);

        // The whole captured page, not the matcher's two: a person picking a series from this
        // list used to see only the first five MangaUpdates offered.
        outcome.Results.Count.ShouldBe(5);
        candidates.Count.ShouldBe(2);

        // One request between the two readers, asking for the configured fallback length.
        host.MangaUpdatesPageSizes.ShouldBe([20]);
    }

    [Fact]
    public async Task SearchAsync_AniListThrottlingTwice_IsA503WithRetryAfterAndSparesTheFallback()
    {
        using var host = new SearchHost(HttpStatusCode.TooManyRequests);

        var thrown = await Should.ThrowAsync<HttpRequestException>(host.SearchAsync);

        // The rate-limit handler waited and retried once; the second 429 reaches the central
        // handler, which used to answer it with a 500 "Unexpected failure" logged as an error.
        host.AniListCalls.ShouldBe(2);
        ProblemDetailsExceptionHandler.Describe(thrown).StatusCode.ShouldBe(503);
        ProblemDetailsExceptionHandler.RetryAfter(thrown).ShouldBe(TimeSpan.FromMinutes(1));

        // Throttling is not an outage: MangaUpdates' budget was not spent on it.
        host.MangaUpdatesPageSizes.ShouldBeEmpty();
    }

    /// <summary>
    /// The composition root with the primary handler of every typed client replaced: AniList
    /// answers the given failure status, MangaUpdates its captured Youjo Senki search.
    /// </summary>
    private sealed class SearchHost : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _mangaUpdatesSearch = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", MangaUpdatesFixtures.YoujoSenkiSearch));

        private int _aniListCalls;

        public SearchHost(HttpStatusCode aniListStatus, IReadOnlyDictionary<string, string?>? settings = null)
        {
            // The shortest back-off the options allow, so the one honoured 429 costs a second.
            var configuration = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
            {
                ["AniList:MaximumRetryDelay"] = "00:00:01",
            };

            var services = CompositionRootTests.BuildServices(configuration);
            services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(
                () => new AnsweringHandler((request, cancellationToken) => AnswerAsync(request, aniListStatus, cancellationToken))));

            _provider = services.BuildServiceProvider();
        }

        public int AniListCalls => _aniListCalls;

        public ConcurrentQueue<int> MangaUpdatesPageSizes { get; } = new();

        public async Task<SeriesSearchOutcome> SearchAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISeriesSearchService>()
                .SearchAsync(Query, Format.LightNovel, CancellationToken.None);
        }

        public async Task<IReadOnlyList<ReleaseSeriesCandidate>> LinkCandidatesAsync()
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IReleaseSource>()
                .SearchAsync(Query, Format.LightNovel, CancellationToken.None);
        }

        public void Dispose() => _provider.Dispose();

        private async Task<HttpResponseMessage> AnswerAsync(
            HttpRequestMessage request, HttpStatusCode aniListStatus, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "graphql.anilist.co")
            {
                Interlocked.Increment(ref _aniListCalls);
                return new HttpResponseMessage(aniListStatus)
                {
                    Content = new StringContent(
                        """{"errors":[{"message":"The AniList API has been temporarily disabled.","status":403}],"data":null}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (uri.Host == "api.mangaupdates.com" && uri.AbsolutePath.EndsWith("/series/search", StringComparison.Ordinal))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(body);
                MangaUpdatesPageSizes.Enqueue(document.RootElement.GetProperty("perpage").GetInt32());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_mangaUpdatesSearch, Encoding.UTF8, "application/json"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private sealed class AnsweringHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            answer(request, cancellationToken);
    }
}
