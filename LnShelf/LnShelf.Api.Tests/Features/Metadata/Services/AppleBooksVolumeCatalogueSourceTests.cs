using System.Net;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The store's allowance pays for requests the store was sent, not for answers the cache held.
/// </summary>
// The nightly allowance is shared by every user's shelf, and the catalogue answer is cached a
// day under the title, so two shelves holding one series cost one request. The arbiter used to
// spend before it asked, cache or not, and users late in the night ran short of requests that
// were never sent.
public sealed class AppleBooksVolumeCatalogueSourceTests
{
    private const int Allowance = 10;

    [Fact]
    public async Task AskAsync_TwoShelvesWithTheSameSeries_SpendOneRequestBetweenThem()
    {
        using var upstream = new CountingHandler();
        var arbiter = CreateArbiter(upstream);
        var budget = new LookupBudget(Allowance);

        await arbiter.AskAsync(SoloLeveling(), Run(), budget, CancellationToken.None);
        var second = await arbiter.AskAsync(SoloLeveling(), Run(), budget, CancellationToken.None);

        upstream.Calls.ShouldBe(1);
        budget.Remaining.ShouldBe(Allowance - 1);

        // And the cached answer is still an answer.
        second.ByVolumeNumber.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AskAsync_ARequestThatFails_StillSpendsIt()
    {
        using var upstream = new CountingHandler(HttpStatusCode.ServiceUnavailable);
        var budget = new LookupBudget(Allowance);

        await CreateArbiter(upstream).AskAsync(SoloLeveling(), Run(), budget, CancellationToken.None);

        upstream.Calls.ShouldBe(1);
        budget.Remaining.ShouldBe(Allowance - 1);
    }

    private static CatalogueTitleArbiter CreateArbiter(HttpMessageHandler upstream)
    {
        var options = Options.Create(new AppleBooksOptions());
        var httpClient = new HttpClient(upstream, disposeHandler: false) { BaseAddress = options.Value.Endpoint };
        var client = new AppleBooksClient(httpClient, options, NullLogger<AppleBooksClient>.Instance);

        // A real HybridCache: whether an answer cost a request is a fact about the cache.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();

        var source = new AppleBooksVolumeCatalogueSource(
            client, cache, options, NullLogger<AppleBooksVolumeCatalogueSource>.Instance);

        return new CatalogueTitleArbiter(
            source,
            new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance),
            NullLogger<CatalogueTitleArbiter>.Instance);
    }

    /// <summary>One shelf's copy of the series: a fresh row each time, as two users have.</summary>
    private static Series SoloLeveling()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            Format = Format.Manga,
            TitleEnglish = "Solo Leveling",
        };

        for (var number = 1; number <= 13; number++)
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        return series;
    }

    private static SyncRun Run() => new() { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };

    /// <summary>Answers the captured Solo Leveling search, and counts how often it was asked.</summary>
    private sealed class CountingHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _json = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", AppleBooksFixtures.SoloLevelingSearch));

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;

            // The store's own content type, which is not JSON (AppleBooksClient says why).
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "text/javascript"),
            });
        }
    }
}
