namespace LnShelf.Client.Features.Library;

/// <summary>
/// How a tap on a status chip changes the selection. Several can be on at once; All is
/// exclusive, and turning the last one off leaves All.
/// </summary>
public static class LibraryFilterSelection
{
    public static IReadOnlyList<LibraryFilter> Toggle(IReadOnlyList<LibraryFilter> current, LibraryFilter filter)
    {
        // All is not one more filter to add; it is "no filter", so it replaces the rest.
        if (filter == LibraryFilter.All)
        {
            return LibraryFilterSet.Everything;
        }

        var chosen = current.Where(status => status != LibraryFilter.All).ToList();
        if (!chosen.Remove(filter))
        {
            chosen.Add(filter);
        }

        return LibraryFilterSet.Normalize(chosen);
    }

    /// <summary>The selection with one filter taken off: the removable chip under the search bar.</summary>
    public static IReadOnlyList<LibraryFilter> Remove(IReadOnlyList<LibraryFilter> current, LibraryFilter filter) =>
        LibraryFilterSet.Normalize(current.Where(status => status != filter));
}
