using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The Wikipedia source's own bookkeeping: when it asks nothing, what it remembers, and that a
/// remembered page is believed only while it still verifies.
/// </summary>
public sealed class WikipediaChapterMapSourceTests
{
    [Fact]
    public async Task GetChapterMapAsync_UnknownSeries_FindsVerifiesAndRemembersThePage()
    {
        var wikimedia = ChainsawManWikimedia();
        var series = ChainsawMan(WikipediaFixtures.ChainsawManVolumeOneIsbn);

        var answer = await Source(wikimedia).GetChapterMapAsync(series, CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Found);
        answer.ChaptersByVolume[24][0].ShouldBe(223m);
        series.ChapterListPage.ShouldBe(WikipediaFixtures.ChainsawManPageTitle);
    }

    // The page learned once saves the Wikidata lookups and the search on every later check.
    [Fact]
    public async Task GetChapterMapAsync_RememberedPage_ReadsOnlyThatPage()
    {
        var wikimedia = ChainsawManWikimedia();
        var series = ChainsawMan(WikipediaFixtures.ChainsawManVolumeOneIsbn);
        series.ChapterListPage = WikipediaFixtures.ChainsawManPageTitle;

        var answer = await Source(wikimedia).GetChapterMapAsync(series, CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Found);
        wikimedia.Requests.ShouldHaveSingleItem().Query.ShouldContain("action=parse");
    }

    // Edited by anybody: a page that no longer holds the series' ISBNs is forgotten at once.
    [Fact]
    public async Task GetChapterMapAsync_RememberedPageNoLongerVerifies_ForgetsIt()
    {
        var series = ChainsawMan("9780000000002");
        series.ChapterListPage = WikipediaFixtures.ChainsawManPageTitle;

        var answer = await Source(ChainsawManWikimedia()).GetChapterMapAsync(series, CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Unmatched);
        series.ChapterListPage.ShouldBeNull();
    }

    [Fact]
    public async Task GetChapterMapAsync_NoIsbnOnTheShelf_AsksNothing()
    {
        var wikimedia = ChainsawManWikimedia();

        var answer = await Source(wikimedia).GetChapterMapAsync(ChainsawMan(isbn: null), CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Unmatched);
        wikimedia.Requests.ShouldBeEmpty();
    }

    // A discovery costs up to seven requests; with fewer left it is not started at all.
    [Fact]
    public async Task GetChapterMapAsync_AllowanceBelowOneDiscovery_IsSkippedWithoutARequest()
    {
        var wikimedia = ChainsawManWikimedia();

        var answer = await Source(wikimedia, maximumRequestsPerRun: 6)
            .GetChapterMapAsync(ChainsawMan(WikipediaFixtures.ChainsawManVolumeOneIsbn), CancellationToken.None);

        answer.Kind.ShouldBe(ChapterMapAnswerKind.Skipped);
        wikimedia.Requests.ShouldBeEmpty();
    }

    private static CapturedWikimedia ChainsawManWikimedia() => new(new WikimediaCaptures(
        ItemSearch: WikipediaFixtures.ChainsawManItemSearch,
        Claims: WikipediaFixtures.ChainsawManClaims,
        PageSearch: WikipediaFixtures.ChainsawManPageSearch,
        Pages: new Dictionary<string, string> { [WikipediaFixtures.ChainsawManPageTitle] = WikipediaFixtures.ChainsawManPage }));

    private static WikipediaChapterMapSource Source(CapturedWikimedia wikimedia, int maximumRequestsPerRun = 50)
    {
        var client = wikimedia.Client();

        return new WikipediaChapterMapSource(
            client,
            new WikipediaListPageFinder(client, NullLogger<WikipediaListPageFinder>.Instance),
            Options.Create(new WikipediaOptions { MaximumRequestsPerRun = maximumRequestsPerRun }),
            NullLogger<WikipediaChapterMapSource>.Instance);
    }

    private static Series ChainsawMan(string? isbn)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = MangaDexFixtures.ChainsawManAniListId,
            Format = Format.Manga,
            TitleEnglish = "Chainsaw Man",
        };
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 1, Isbn = isbn });

        return series;
    }
}
