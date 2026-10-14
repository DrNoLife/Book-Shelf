namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Which table of a Wikipedia page is this series — by ISBN, never by title — and the chapters
/// per volume it gives. Pure.
/// </summary>
// A page is found by a search that only proposes (WikipediaListPageFinder), and a franchise's
// page holds several tables: Slime's has the light novels and six manga. Only an English
// edition ISBN the shelf already holds for this series ties a table to it, the same standard as
// MangaDexSeriesMatcher's links. A miss is accepted as the price: where the shelf holds the
// e-book's ISBN and the page the print one's, MangaDex maps the series instead.
public static class GraphicNovelListVerifier
{
    /// <param name="tables">The page's tables, in page order.</param>
    /// <param name="seriesIsbns">The series' volumes' ISBNs, as 13 digits.</param>
    /// <returns>The table sharing the most ISBNs with the series, or null when none shares one.</returns>
    public static GraphicNovelListTable? Pick(IReadOnlyList<GraphicNovelListTable> tables, IReadOnlySet<string> seriesIsbns)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(seriesIsbns);

        var best = tables
            .Select(table => (Table: table, Shared: table.Entries.Count(entry => entry.LicensedIsbns.Any(seriesIsbns.Contains))))
            .Where(candidate => candidate.Shared > 0)
            .OrderByDescending(candidate => candidate.Shared)
            .FirstOrDefault();

        return best.Table;
    }

    /// <summary>
    /// The verified table as the chapter-map seam's raw answer: whole volume number to chapters.
    /// </summary>
    // A bonus volume's row (8.5) and a row without numbered chapters are left out, exactly as
    // the seam expects: the builder and the merge decide what a gap means.
    public static IReadOnlyDictionary<int, IReadOnlyList<decimal>> ToChaptersByVolume(GraphicNovelListTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var chaptersByVolume = new Dictionary<int, IReadOnlyList<decimal>>();

        foreach (var entry in table.Entries)
        {
            if (entry is { VolumeNumber: { } number, Chapters: { Count: > 0 } chapters }
                && number >= 1
                && !number.IsBonusVolume)
            {
                chaptersByVolume.TryAdd((int)number, chapters);
            }
        }

        return chaptersByVolume;
    }
}
