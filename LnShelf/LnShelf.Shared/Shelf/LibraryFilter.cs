namespace LnShelf.Shared.Shelf;

/// <summary>
/// The status filter behind the library's chips. Mixes user tracking status with
/// derived state deliberately — that is what the shelves in the sidebar are. Several can be
/// chosen at once; a series shows when it fits any of them (<see cref="LibraryFilterSet"/>).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibraryFilter>))]
public enum LibraryFilter
{
    All,

    /// <summary>At least one volume owned: everything on the reader's actual shelf.</summary>
    // Stored and sent by name everywhere (preferences column, query string, JSON), so placing
    // it here, beside All, renumbers nothing that anything keeps.
    Owned,

    /// <summary>Anything to buy or anything to read.</summary>
    Behind,

    /// <summary>Owned and unread.</summary>
    Backlog,

    /// <summary>Nothing to buy and nothing to read.</summary>
    UpToDate,

    Reading,
    Completed,
    PlanToStart,
    Dropped,
}
