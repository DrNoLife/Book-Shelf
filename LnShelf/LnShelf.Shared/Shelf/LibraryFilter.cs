namespace LnShelf.Shared.Shelf;

/// <summary>
/// The status filter behind the library's chips. Mixes user tracking status with
/// derived state deliberately — that is what the shelves in the sidebar are.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<LibraryFilter>))]
public enum LibraryFilter
{
    All,

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
