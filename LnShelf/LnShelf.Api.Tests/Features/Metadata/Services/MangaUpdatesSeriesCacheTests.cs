using System.Net;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// One <c>series/{id}</c> request behind both readers of it: the add flow reads the record,
/// and the refresh that completes the add reads it again through the release source.
/// </summary>
// Uncached, adding a series from MangaUpdates fetched the same record twice within seconds,
// and every shelf tracking it fetched it again at night (CLAUDE.md — be a polite API citizen).
public sealed class MangaUpdatesSeriesCacheTests
{
    [Fact]
    public async Task GetSeriesAsync_TheAddFlowThenTheRefresh_ReachesTheSourceOnce()
    {
        using var upstream = new CountingHandler(HttpStatusCode.OK);
        var (releaseSource, queryService) = CreateServices(upstream);

        var metadata = await queryService.GetSeriesAsync(
            MangaUpdatesFixtures.SoloLevelingSeriesId, CancellationToken.None);
        var info = await releaseSource.GetSeriesAsync(
            MangaUpdatesFixtures.SoloLevelingSeriesId, CancellationToken.None);

        upstream.Calls.ShouldBe(1);

        // The cached copy maps to what the wire record maps to: nothing is lost on the way
        // through the cache's serializer.
        var direct = MangaUpdatesSeriesMapper.ToSeriesInfo(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.SoloLevelingSeries));
        info.ShouldNotBeNull().VolumeCount.ShouldBe(direct.VolumeCount);
        info.ExtraVolumeCount.ShouldBe(direct.ExtraVolumeCount);
        metadata.ShouldNotBeNull().TitleEnglish.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSeriesAsync_AFailedRequest_IsAskedAgainRatherThanRemembered()
    {
        using var upstream = new CountingHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        var (releaseSource, _) = CreateServices(upstream);

        await Should.ThrowAsync<HttpRequestException>(() => releaseSource.GetSeriesAsync(
            MangaUpdatesFixtures.SoloLevelingSeriesId, CancellationToken.None));
        var info = await releaseSource.GetSeriesAsync(
            MangaUpdatesFixtures.SoloLevelingSeriesId, CancellationToken.None);

        upstream.Calls.ShouldBe(2);
        info.ShouldNotBeNull();
    }

    private static (MangaUpdatesReleaseSource ReleaseSource, MangaUpdatesSeriesQueryService QueryService)
        CreateServices(HttpMessageHandler upstream)
    {
        var options = Options.Create(new MangaUpdatesOptions());
        var httpClient = new HttpClient(upstream, disposeHandler: false) { BaseAddress = options.Value.Endpoint };
        var client = new MangaUpdatesClient(httpClient, NullLogger<MangaUpdatesClient>.Instance);

        // A real HybridCache: what is under test is that two callers share one entry, which
        // is a fact about the cache and not about a substitute.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        var searchCache = new MangaUpdatesSearchCache(
            client, cache, options, NullLogger<MangaUpdatesSearchCache>.Instance);
        var seriesCache = new MangaUpdatesSeriesCache(client, cache, options);

        return (
            new MangaUpdatesReleaseSource(searchCache, seriesCache, options, NullLogger<MangaUpdatesReleaseSource>.Instance),
            new MangaUpdatesSeriesQueryService(searchCache, seriesCache, options, NullLogger<MangaUpdatesSeriesQueryService>.Instance));
    }

    /// <summary>
    /// Answers the captured Solo Leveling record, with the given statuses in turn (the last
    /// one repeating), and counts how often it was asked.
    /// </summary>
    private sealed class CountingHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly string _json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", MangaUpdatesFixtures.SoloLevelingSeries));

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var status = statuses[Math.Min(Calls, statuses.Length - 1)];
            Calls++;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
