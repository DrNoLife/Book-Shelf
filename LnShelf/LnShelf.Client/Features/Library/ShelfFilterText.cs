namespace LnShelf.Client.Features.Library;

/// <summary>
/// The words for the shelf's filters, shared by the status chips in the filter panel and the
/// removable chips under the search bar, so the two can never name a filter differently.
/// </summary>
// Terminology is fixed (design.md, Voice): To buy / Backlog / Behind / Up to date are never
// paraphrased.
public static class ShelfFilterText
{
    public static string StatusLabel(LibraryFilter filter) => filter switch
    {
        LibraryFilter.All => "All",
        LibraryFilter.Behind => "Behind",
        LibraryFilter.Backlog => "Backlog",
        LibraryFilter.UpToDate => "Up to date",
        LibraryFilter.Reading => "Reading",
        LibraryFilter.Completed => "Completed",
        LibraryFilter.PlanToStart => "Plan to start",
        LibraryFilter.Dropped => "Dropped",
        _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown library filter."),
    };

    /// <summary>Spelled out: a chip on its own has no "LN · Manga · WN" row beside it to explain "LN".</summary>
    public static string FormatLabel(Format format) => format switch
    {
        Format.LightNovel => "Light novels",
        Format.Manga => "Manga",
        Format.WebNovel => "Web novels",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };
}
