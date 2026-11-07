using System.Net;
using System.Text.Json;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// AniList behind <c>ISeriesMetadataSource</c>, against its captured answers: what the search
/// asks for, what never becomes a request at all, and what a missing id reads as.
/// </summary>
public sealed class AniListSeriesQueryServiceTests
{
    [Theory]
    [InlineData(Format.LightNovel, new[] { "NOVEL" })]
    [InlineData(Format.Manga, new[] { "MANGA", "ONE_SHOT" })]
    [InlineData(null, new[] { "NOVEL", "MANGA", "ONE_SHOT" })]
    public async Task SearchAsync_AFormat_AsksForItsAniListFormatsAndTheConfiguredPageSize(
        Format? format, string[] expectedFormats)
    {
        using var upstream = new CapturingHandler(AniListFixtures.MushokuSearch);

        await CreateService(upstream, searchResultLimit: 7).SearchAsync("mushoku", format, CancellationToken.None);

        var variables = upstream.Variables.ShouldHaveSingleItem();
        variables.GetProperty("search").GetString().ShouldBe("mushoku");
        variables.GetProperty("perPage").GetInt32().ShouldBe(7);
        variables.GetProperty("formats").EnumerateArray().Select(element => element.GetString())
            .ShouldBe(expectedFormats);
    }

    [Fact]
    public async Task SearchAsync_TheCapturedAnswer_MapsTrackableHits()
    {
        using var upstream = new CapturingHandler(AniListFixtures.MushokuSearch);

        var results = await CreateService(upstream).SearchAsync("mushoku", null, CancellationToken.None);

        results.ShouldNotBeEmpty();
        results.ShouldAllBe(result => result.Source == MetadataSourceName.AniList);
        results.ShouldContain(result => result.SourceId == AniListFixtures.MushokuTenseiNovelId);
    }

    [Fact]
    public async Task SearchAsync_TheSameQueryInAnotherCase_IsAnsweredFromTheCache()
    {
        using var upstream = new CapturingHandler(AniListFixtures.MushokuSearch);
        var service = CreateService(upstream);

        await service.SearchAsync("Mushoku", Format.LightNovel, CancellationToken.None);
        await service.SearchAsync(" mushoku ", Format.LightNovel, CancellationToken.None);

        upstream.Variables.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("m", Format.LightNovel)]
    [InlineData("  ", null)]
    [InlineData("mushoku", Format.WebNovel)]
    public async Task SearchAsync_AQueryAniListCannotUsefullyAnswer_NeverBecomesARequest(string query, Format? format)
    {
        // One letter would spend the budget on prefixes, and AniList has no web novels.
        using var upstream = new CapturingHandler(AniListFixtures.MushokuSearch);

        var results = await CreateService(upstream).SearchAsync(query, format, CancellationToken.None);

        results.ShouldBeEmpty();
        upstream.Variables.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSeriesAsync_TheCapturedDetail_MapsTheSeries()
    {
        using var upstream = new CapturingHandler(AniListFixtures.MushokuTenseiNovel);

        var series = await CreateService(upstream)
            .GetSeriesAsync(AniListFixtures.MushokuTenseiNovelId, CancellationToken.None);

        series.ShouldNotBeNull().ExternalId.ShouldBe(AniListFixtures.MushokuTenseiNovelId);
        upstream.Variables.ShouldHaveSingleItem().GetProperty("id").GetInt32()
            .ShouldBe(AniListFixtures.MushokuTenseiNovelId);
    }

    [Fact]
    public async Task GetSeriesAsync_AnIdAniListDoesNotKnow_IsNullRatherThanAFailure()
    {
        // AniList answers a missing id with a 404 carrying a GraphQL body: data, not an outage.
        using var upstream = new CapturingHandler(fixtureFileName: null);

        var series = await CreateService(upstream).GetSeriesAsync(999999999, CancellationToken.None);

        series.ShouldBeNull();
    }

    private static AniListSeriesQueryService CreateService(HttpMessageHandler upstream, int searchResultLimit = 20)
    {
        var options = new AniListOptions { SearchResultLimit = searchResultLimit };
        var httpClient = new HttpClient(upstream, disposeHandler: false) { BaseAddress = options.Endpoint };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();

        return new AniListSeriesQueryService(
            new AniListGraphQlClient(httpClient, NullLogger<AniListGraphQlClient>.Instance),
            services.BuildServiceProvider().GetRequiredService<HybridCache>(),
            Options.Create(options),
            NullLogger<AniListSeriesQueryService>.Instance);
    }

    /// <summary>
    /// Answers one captured response (or AniList's 404 for a missing id when given none) and
    /// keeps the variables each request sent.
    /// </summary>
    private sealed class CapturingHandler(string? fixtureFileName) : HttpMessageHandler
    {
        private const string NotFoundBody =
            """{"errors":[{"message":"Not Found.","status":404}],"data":{"Media":null}}""";

        private readonly string? _json = fixtureFileName is null
            ? null
            : File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Features", "Metadata", "Fixtures", fixtureFileName));

        public List<JsonElement> Variables { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            Variables.Add(document.RootElement.GetProperty("variables").Clone());

            return new HttpResponseMessage(_json is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(_json ?? NotFoundBody, Encoding.UTF8, "application/json"),
            };
        }
    }
}
