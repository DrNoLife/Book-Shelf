namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
// English Wikipedia's chapter list pages behind IChapterMapSource, ahead of MangaDex in
// precedence: an edited list of the published volumes rather than a scanlation index, so its
// starts are the book's own. It fills what MangaDex lacks (Chainsaw Man's vols 21–23) and
// MangaDex fills what it cannot number (Berserk's prologue volumes).
//
// Every answer is re-verified by ISBN, the remembered page included: a page is edited by
// anybody, and a table that stops matching the shelf's ISBNs stops being believed at once.
//
// Scoped, with the request allowance as a field, for the same reason as MangaDex's: a scope is
// one sync run or one on-demand refresh.
public sealed class WikipediaChapterMapSource(
    WikipediaClient client,
    WikipediaListPageFinder finder,
    IOptions<WikipediaOptions> options,
    ILogger<WikipediaChapterMapSource> logger) : IChapterMapSource
{
    private readonly LookupBudget _budget = new(options.Value.MaximumRequestsPerRun);

    public MetadataSourceName SourceName => MetadataSourceName.Wikipedia;

    // All or nothing per series, as MangaDex's: a half-asked series would be judged on half an
    // answer. A series this source asks nothing about costs nothing, so it can always answer.
    public bool CanAnswer(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Format is not Format.Manga || IsbnsOf(series).Count == 0)
        {
            return true;
        }

        var cost = series.ChapterListPage is null ? WikipediaListPageFinder.MaximumRequests : 1;
        return _budget.Remaining >= cost;
    }

    public async Task<ChapterMapAnswer> GetChapterMapAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Format is not Format.Manga)
        {
            return ChapterMapAnswer.Unmatched("Wikipedia's chapter lists are read for manga only.");
        }

        // Without an ISBN nothing could verify a page, so nothing is asked.
        var isbns = IsbnsOf(series);
        if (isbns.Count == 0)
        {
            return ChapterMapAnswer.Unmatched("No volume has an ISBN to verify a Wikipedia list against.");
        }

        if (!CanAnswer(series))
        {
            return ChapterMapAnswer.Skipped;
        }

        var verified = series.ChapterListPage is { } remembered
            ? await ReverifyAsync(remembered, isbns, cancellationToken)
            : await finder.FindAsync(series, isbns, _budget, cancellationToken);

        if (verified is null)
        {
            // Forgotten, so the next check looks for the page afresh rather than reading a dead one.
            series.ChapterListPage = null;
            return ChapterMapAnswer.Unmatched("No Wikipedia chapter list shares an ISBN with this series.");
        }

        if (series.ChapterListPage != verified.PageTitle)
        {
            logger.LogInformation(
                "Matched {TitleEnglish} to Wikipedia's \"{PageTitle}\" by ISBN",
                series.TitleEnglish,
                verified.PageTitle);
        }

        series.ChapterListPage = verified.PageTitle;
        return ChapterMapAnswer.Found(GraphicNovelListVerifier.ToChaptersByVolume(verified.Table));
    }

    private static IReadOnlySet<string> IsbnsOf(Series series) =>
        series.Volumes.Select(volume => Isbn.ToIsbn13(volume.Isbn)).OfType<string>().ToHashSet();

    private async Task<VerifiedChapterList?> ReverifyAsync(
        string pageTitle,
        IReadOnlySet<string> isbns,
        CancellationToken cancellationToken)
    {
        var page = await client.GetPageAsync(pageTitle, cancellationToken);
        _budget.Spend(1);

        return page is not null && GraphicNovelListVerifier.Pick(GraphicNovelListParser.Parse(page.Wikitext), isbns) is { } table
            ? new VerifiedChapterList(page.Title, table)
            : null;
    }
}
