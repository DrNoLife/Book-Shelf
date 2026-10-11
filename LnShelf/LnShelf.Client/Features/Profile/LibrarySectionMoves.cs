namespace LnShelf.Client.Features.Profile;

/// <summary>
/// Moving one library section a place up or down, for the profile's section-order list.
/// </summary>
public static class LibrarySectionMoves
{
    /// <summary>
    /// The order with <paramref name="section"/> swapped with its neighbour: -1 is up, +1 down.
    /// A move past either end, or of a section not in the order, changes nothing.
    /// </summary>
    public static IReadOnlyList<LibrarySection> Move(
        IReadOnlyList<LibrarySection> order,
        LibrarySection section,
        int offset)
    {
        var moved = order.ToList();
        var from = moved.IndexOf(section);
        var to = from + offset;

        if (from < 0 || to < 0 || to >= moved.Count)
        {
            return order;
        }

        (moved[from], moved[to]) = (moved[to], moved[from]);
        return moved;
    }

    /// <summary>What the list calls each section: the words its heading uses on the library page.</summary>
    public static string Label(LibrarySection section) => section switch
    {
        LibrarySection.ContinueReading => "Continue reading",
        LibrarySection.OutNow => "Out now",
        LibrarySection.Shelf => "Shelf",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown library section."),
    };
}
