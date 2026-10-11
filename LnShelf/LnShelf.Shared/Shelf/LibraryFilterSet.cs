namespace LnShelf.Shared.Shelf;

/// <summary>
/// A set of status filters, combined the way the shelf combines them: a series shows when it
/// fits any one of them. The server and the client both normalise with this, so the two
/// always agree on what a selection means.
/// </summary>
public static class LibraryFilterSet
{
    /// <summary>Behind is the opinionated default (feature-spec §4.6).</summary>
    public static IReadOnlyList<LibraryFilter> Default { get; } = [LibraryFilter.Behind];

    /// <summary>Nothing filtered: the whole shelf.</summary>
    public static IReadOnlyList<LibraryFilter> Everything { get; } = [LibraryFilter.All];

    /// <summary>
    /// Repeats and unknown values dropped, in the enum's order. All is exclusive (with it in
    /// the set the others add nothing) and an empty set filters nothing, so both come back as
    /// All alone.
    /// </summary>
    public static IReadOnlyList<LibraryFilter> Normalize(IEnumerable<LibraryFilter>? filters)
    {
        List<LibraryFilter> known = [.. (filters ?? []).Where(value => Enum.IsDefined(value)).Distinct().Order()];

        return known.Count == 0 || known.Contains(LibraryFilter.All) ? Everything : known;
    }

    /// <summary>Whether a set filters nothing at all.</summary>
    public static bool IsEverything(IReadOnlyCollection<LibraryFilter> filters) =>
        filters.Count == 0 || filters.Contains(LibraryFilter.All);
}
