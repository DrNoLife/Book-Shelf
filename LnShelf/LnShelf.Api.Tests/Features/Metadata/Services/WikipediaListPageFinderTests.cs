using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Finding a series' chapter list page: Wikidata's "has list" first, the template search after
/// it, and an ISBN on the page as the only proof.
/// </summary>
public sealed class WikipediaListPageFinderTests
{
    // Frieren is the canary whose Wikidata item names its list article.
    [Fact]
    public async Task FindAsync_WikidataNamesAList_ReadsThatPageAndSearchesNothing()
    {
        var wikimedia = new CapturedWikimedia(new WikimediaCaptures(
            ItemSearch: WikipediaFixtures.FrierenItemSearch,
            Claims: WikipediaFixtures.FrierenClaims,
            ListSitelinks: WikipediaFixtures.FrierenListSitelinks,
            Pages: new Dictionary<string, string> { [WikipediaFixtures.FrierenPageTitle] = WikipediaFixtures.FrierenPage }));
        var budget = new LookupBudget(10);

        var found = await Finder(wikimedia).FindAsync(
            Series(WikipediaFixtures.FrierenAniListId, "Frieren: Beyond Journey's End"),
            Isbns(WikipediaFixtures.FrierenVolumeOneIsbn),
            budget,
            CancellationToken.None);

        found.ShouldNotBeNull().PageTitle.ShouldBe(WikipediaFixtures.FrierenPageTitle);
        found.Table.Entries.Count.ShouldBe(15);
        wikimedia.Requests.ShouldNotContain(uri => uri.Host == "en.wikipedia.org" && uri.Query.Contains("list=search"));
        budget.Remaining.ShouldBe(6);
    }

    // Chainsaw Man's item has no "has list"; the template search proposes the list first.
    [Fact]
    public async Task FindAsync_NoListOnWikidata_FallsBackToTheTemplateSearch()
    {
        var wikimedia = ChainsawManWikimedia();
        var budget = new LookupBudget(10);

        var found = await Finder(wikimedia).FindAsync(
            Series(MangaDexFixtures.ChainsawManAniListId, "Chainsaw Man"),
            Isbns(WikipediaFixtures.ChainsawManVolumeOneIsbn),
            budget,
            CancellationToken.None);

        found.ShouldNotBeNull().PageTitle.ShouldBe(WikipediaFixtures.ChainsawManPageTitle);
        wikimedia.Requests.Count.ShouldBe(4);
        budget.Remaining.ShouldBe(6);
    }

    // A title search proves nothing: the right page, read and found without this ISBN, is not taken.
    [Fact]
    public async Task FindAsync_NoCandidateSharesAnIsbn_IsNull()
    {
        var wikimedia = ChainsawManWikimedia();
        var budget = new LookupBudget(10);

        var found = await Finder(wikimedia).FindAsync(
            Series(MangaDexFixtures.ChainsawManAniListId, "Chainsaw Man"),
            Isbns("9780000000002"),
            budget,
            CancellationToken.None);

        found.ShouldBeNull();
        wikimedia.Requests.Count(uri => uri.Query.Contains("action=parse")).ShouldBe(2);
        (10 - budget.Remaining).ShouldBeLessThanOrEqualTo(WikipediaListPageFinder.MaximumRequests);
    }

    private static CapturedWikimedia ChainsawManWikimedia() => new(new WikimediaCaptures(
        ItemSearch: WikipediaFixtures.ChainsawManItemSearch,
        Claims: WikipediaFixtures.ChainsawManClaims,
        PageSearch: WikipediaFixtures.ChainsawManPageSearch,
        Pages: new Dictionary<string, string> { [WikipediaFixtures.ChainsawManPageTitle] = WikipediaFixtures.ChainsawManPage }));

    private static WikipediaListPageFinder Finder(CapturedWikimedia wikimedia) =>
        new(wikimedia.Client(), NullLogger<WikipediaListPageFinder>.Instance);

    private static HashSet<string> Isbns(params string[] isbns) => [.. isbns];

    private static Series Series(int aniListId, string title) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = aniListId,
        Format = Format.Manga,
        TitleEnglish = title,
    };
}
