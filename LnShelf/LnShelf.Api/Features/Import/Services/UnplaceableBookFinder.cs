namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Finds the books in one series that have no volume to land on: a bonus or omnibus
/// volume ("Vol. 10.5", "Vols. 1-2") always, and any other book without a number of its
/// own in a series whose other books are numbered.
/// </summary>
// DesiredVolumeStateMerger reads a missing number as volume 1, which is right for a
// standalone book (and for two editions of one). Beside numbered volumes it is a title
// the parser could not read, and folding it into volume 1 silently overwrote that
// volume's state with another book's (audit 2026-10-09). The commit refuses instead.
//
// A bonus or omnibus volume is refused even with no numbered sibling: the parser gives it
// the clean series name, so an export holding only "Overlord, Vols. 1-2" auto-matches
// Overlord and would otherwise write the omnibus onto volume 1.
public static class UnplaceableBookFinder
{
    /// <returns>The raw titles of the unplaceable books, or none.</returns>
    public static IReadOnlyList<string> Find(IEnumerable<ImportGroup> seriesGroups)
    {
        ArgumentNullException.ThrowIfNull(seriesGroups);

        var rows = seriesGroups.SelectMany(group => group.Rows).ToList();
        var hasNumberedVolume = rows.Any(row => row.VolumeNumber is not null);

        return
        [
            .. rows
                .Where(row => row.VolumeNumber is null
                              && (hasNumberedVolume || BookTitleParser.HasUnnumberableVolume(row.RawTitle)))
                .Select(row => row.RawTitle),
        ];
    }
}
