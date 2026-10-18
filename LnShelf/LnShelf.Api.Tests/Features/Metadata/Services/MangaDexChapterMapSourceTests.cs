using System.Net;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The MangaDex source's own bookkeeping: what it asks, what it remembers, and when its
/// allowance says not to ask at all. Matching and mapping have their own tests.
/// </summary>
public sealed class MangaDexChapterMapSourceTests
{
    [Fact]
    public async Task GetChapterMapAsync_UnknownSeries_SearchesRemembersTheIdAndAnswersItsChapters()
    {
        var requests = new List<Uri>();
        var source = Source(requests, maximumRequestsPerRun: 10);
        var series = ChainsawMan();

        var answer = await source.GetChapterMapAsync(series, CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Found);
        answer.ChaptersByVolume[1].ShouldContain(7m);
        series.MangaDexId.ShouldBe(MangaDexFixtures.ChainsawManId);
        requests.Count.ShouldBe(3);
    }

    // The id learned once saves the title search on every later check.
    [Fact]
    public async Task GetChapterMapAsync_RememberedId_AsksOnlyForTheTwoAggregates()
    {
        var requests = new List<Uri>();
        var series = ChainsawMan();
        series.MangaDexId = MangaDexFixtures.ChainsawManId;

        await Source(requests, maximumRequestsPerRun: 10).GetChapterMapAsync(series, CancellationToken.None);

        requests.ShouldAllBe(uri => uri.AbsolutePath.EndsWith("/aggregate"));
        requests.Count.ShouldBe(2);
    }

    // A series costs up to four requests; with fewer left it is not started at all.
    [Fact]
    public async Task GetChapterMapAsync_AllowanceBelowOneSeries_IsSkippedWithoutARequest()
    {
        var requests = new List<Uri>();
        var source = Source(requests, maximumRequestsPerRun: 3);

        source.CanAnswer(ChainsawMan()).ShouldBeFalse();
        var answer = await source.GetChapterMapAsync(ChainsawMan(), CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Skipped);
        requests.ShouldBeEmpty();
    }

    // A remembered record MangaDex no longer has answers 404 for good: the link is forgotten so
    // the next check matches afresh, and the answer is Unmatched rather than a failure.
    [Fact]
    public async Task GetChapterMapAsync_RememberedRecordIsGone_ForgetsTheIdAndIsUnmatched()
    {
        var requests = new List<Uri>();
        var series = ChainsawMan();
        series.MangaDexId = MangaDexFixtures.ChainsawManId;

        var answer = await Source(requests, maximumRequestsPerRun: 10, aggregateStatus: HttpStatusCode.NotFound)
            .GetChapterMapAsync(series, CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Unmatched);
        series.MangaDexId.ShouldBeNull();
        requests.Count.ShouldBe(1);
    }

    private static MangaDexChapterMapSource Source(
        List<Uri> requests,
        int maximumRequestsPerRun,
        HttpStatusCode aggregateStatus = HttpStatusCode.OK)
    {
        var httpClient = new HttpClient(new CapturedAnswers(requests, aggregateStatus)) { BaseAddress = new Uri("https://api.mangadex.org/") };

        return new MangaDexChapterMapSource(
            new MangaDexClient(httpClient, NullLogger<MangaDexClient>.Instance),
            Options.Create(new MangaDexOptions { MaximumRequestsPerRun = maximumRequestsPerRun }),
            NullLogger<MangaDexChapterMapSource>.Instance);
    }

    private static Series ChainsawMan() => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = MangaDexFixtures.ChainsawManAniListId,
        Format = Format.Manga,
        TitleEnglish = "Chainsaw Man",
    };

    private sealed class CapturedAnswers(List<Uri> requests, HttpStatusCode aggregateStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            requests.Add(uri);

            var fixture = MangaDexFixtures.ChainsawManSearch;
            if (uri.AbsolutePath.EndsWith("/aggregate", StringComparison.Ordinal))
            {
                if (aggregateStatus is not HttpStatusCode.OK)
                {
                    return Task.FromResult(new HttpResponseMessage(aggregateStatus));
                }

                fixture = uri.Query.Contains("translatedLanguage", StringComparison.Ordinal)
                    ? MangaDexFixtures.ChainsawManEnglish
                    : MangaDexFixtures.ChainsawManAll;
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(MangaDexFixtures.Read(fixture), Encoding.UTF8, "application/json"),
            });
        }
    }
}
