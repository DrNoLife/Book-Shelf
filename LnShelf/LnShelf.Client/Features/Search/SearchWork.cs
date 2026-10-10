namespace LnShelf.Client.Features.Search;

/// <summary>
/// One work in the search results: every series whose titles share a base title — the light
/// novel, its manga, their parts and the side stories — gathered under that title.
/// </summary>
public sealed record SearchWork
{
    /// <summary>The base title: "Ascendance of a Bookworm".</summary>
    public required string Title { get; init; }

    /// <summary>"Honzuki no Gekokujou · 本好きの下剋上". Null when the source gave none.</summary>
    public string? AlternativeTitles { get; init; }

    /// <summary>The base title itself and its numbered parts or seasons, in reading order.</summary>
    public required IReadOnlyList<SearchWorkEntry> MainStory { get; init; }

    /// <summary>Everything else under the base title, oldest first.</summary>
    public required IReadOnlyList<SearchWorkEntry> SideStories { get; init; }

    /// <summary>A work of one series renders as a plain card with one row and no group labels.</summary>
    public bool IsSingle => MainStory.Count + SideStories.Count == 1;

    /// <summary>Every entry, main story first.</summary>
    public IEnumerable<SearchWorkEntry> Entries => MainStory.Concat(SideStories);

    /// <summary>How many of this work's series are of one format.</summary>
    public int CountOf(Format format) => Entries.Count(entry => entry.Result.Format == format);
}

/// <summary>
/// One series inside a work, named the way the work card prints it.
/// </summary>
public sealed record SearchWorkEntry
{
    public required SeriesSearchResultResponse Result { get; init; }

    /// <summary>What the row says first: "Part 1", or what the title adds to the base title.</summary>
    public required string Name { get; init; }

    /// <summary>The second line: the rest of the title, or another script's title. Null for none.</summary>
    public string? Subtitle { get; init; }

    /// <summary>The part or season this entry is, when its title numbers it.</summary>
    public int? PartNumber { get; init; }

    /// <summary>
    /// The name is the format ("Light novel", "Manga"), because the title is the work's own
    /// title and the header already says it. A row then needs no " · LN" after it.
    /// </summary>
    public bool IsNamedByFormat { get; init; }
}
