namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Finds the books in one series that have no volume to land on: no number of their own,
/// in a series whose other books are numbered.
/// </summary>
// DesiredVolumeStateMerger reads a missing number as volume 1, which is right for a
// standalone book (and for two editions of one). Beside numbered volumes it is a title
// the parser could not read, and folding it into volume 1 silently overwrote that
// volume's state with another book's (audit 2026-10-09). The commit refuses instead.
public static class UnplaceableBookFinder
{
    /// <returns>The raw titles of the unplaceable books, or none.</returns>
    public static IReadOnlyList<string> Find(IEnumerable<ImportGroup> seriesGroups)
    {
        ArgumentNullException.ThrowIfNull(seriesGroups);

        var rows = seriesGroups.SelectMany(group => group.Rows).ToList();

        if (!rows.Any(row => row.VolumeNumber is not null))
        {
            return [];
        }

        return [.. rows.Where(row => row.VolumeNumber is null).Select(row => row.RawTitle)];
    }
}
