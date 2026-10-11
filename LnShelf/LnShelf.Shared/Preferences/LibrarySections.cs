namespace LnShelf.Shared.Preferences;

/// <summary>
/// The order the library page's sections are drawn in. The server and the client both
/// normalise with it, so a stored order can never leave a section off the page.
/// </summary>
public static class LibrarySections
{
    /// <summary>The page as it was designed: what to read, what to buy, then the shelf.</summary>
    public static IReadOnlyList<LibrarySection> Default { get; } =
        [LibrarySection.ContinueReading, LibrarySection.OutNow, LibrarySection.Shelf];

    /// <summary>
    /// Every section exactly once: the given order with repeats and unknown values dropped,
    /// and any section it leaves out appended in the default order.
    /// </summary>
    // Tolerant rather than strict because a stored order outlives the enum: a section added
    // later is missing from every order saved before it, and must still appear.
    public static IReadOnlyList<LibrarySection> Normalize(IEnumerable<LibrarySection>? order) =>
        [.. (order ?? []).Where(value => Enum.IsDefined(value)).Concat(Default).Distinct()];
}
