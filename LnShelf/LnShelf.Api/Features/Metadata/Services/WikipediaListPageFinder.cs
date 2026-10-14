namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Finds the English Wikipedia page that lists this series' chapters, and the table on it whose
/// ISBNs prove it is this series.
/// </summary>
// Candidates come from two places, and neither is trusted on its own (GraphicNovelListVerifier):
//
//   1. Wikidata. The item carrying this series' AniList id, and the list article its "has list"
//      statement points at. Exact when present, but present only sometimes (Frieren, of the six
//      canaries).
//   2. A search of the pages using the Graphic novel list template for the English title. It
//      found all five canaries that have a list page, the right page first every time.
//
// A search by ISBN was tried in the spike and left out: Wikipedia indexes the hyphenated form,
// the shelf holds digits, and hyphenating an ISBN needs the registration ranges.
public sealed class WikipediaListPageFinder(WikipediaClient client, ILogger<WikipediaListPageFinder> logger)
{
    /// <summary>Search results read, best first. The right page has always been the first.</summary>
    private const int SearchCandidates = 2;

    /// <summary>
    /// The most one discovery can cost: the Wikidata item, its list statement and the list's
    /// article (3), the search (1), and a read of up to three candidate pages (3).
    /// </summary>
    public const int MaximumRequests = 7;

    /// <returns>The verified page and table, or null when no candidate shares an ISBN with the series.</returns>
    public async Task<VerifiedChapterList?> FindAsync(
        Series series,
        IReadOnlySet<string> seriesIsbns,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(seriesIsbns);
        ArgumentNullException.ThrowIfNull(budget);

        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (await FromWikidataAsync(series, budget, cancellationToken) is { } listPage
            && await VerifyAsync(listPage, seriesIsbns, tried, budget, cancellationToken) is { } fromWikidata)
        {
            return fromWikidata;
        }

        var title = new[] { series.TitleEnglish, series.TitleRomaji }.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        if (title is null)
        {
            return null;
        }

        var proposed = await client.SearchListPagesAsync(title, SearchCandidates, cancellationToken);
        budget.Spend(1);

        foreach (var pageTitle in proposed.Take(SearchCandidates))
        {
            if (await VerifyAsync(pageTitle, seriesIsbns, tried, budget, cancellationToken) is { } fromSearch)
            {
                return fromSearch;
            }
        }

        return null;
    }

    /// <returns>The title of the list article Wikidata links to this series, or null.</returns>
    private async Task<string?> FromWikidataAsync(Series series, LookupBudget budget, CancellationToken cancellationToken)
    {
        if (series.AniListId is not { } aniListId)
        {
            return null;
        }

        var item = await client.FindItemByAniListIdAsync(aniListId, cancellationToken);
        budget.Spend(1);
        if (item is null)
        {
            return null;
        }

        var lists = await client.GetListItemsAsync(item, cancellationToken);
        budget.Spend(1);
        if (lists.Count == 0)
        {
            return null;
        }

        // The first statement only: a work with several lists (chapters, episodes) states the
        // chapters first in every case seen, and a wrong one fails verification anyway.
        var title = await client.GetEnglishArticleTitleAsync(lists[0], cancellationToken);
        budget.Spend(1);

        return title;
    }

    private async Task<VerifiedChapterList?> VerifyAsync(
        string pageTitle,
        IReadOnlySet<string> seriesIsbns,
        HashSet<string> tried,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        if (!tried.Add(pageTitle))
        {
            return null;
        }

        var page = await client.GetPageAsync(pageTitle, cancellationToken);
        budget.Spend(1);

        if (page is null || GraphicNovelListVerifier.Pick(GraphicNovelListParser.Parse(page.Wikitext), seriesIsbns) is not { } table)
        {
            logger.LogInformation("Wikipedia page \"{PageTitle}\" lists none of this series' ISBNs", pageTitle);
            return null;
        }

        // Remembered under the page's own title, so a redirect is followed once, not every week.
        tried.Add(page.Title);
        return new VerifiedChapterList(page.Title, table);
    }
}
