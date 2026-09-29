namespace LnShelf.Shared.Preferences;

/// <summary>
/// How tightly the library grid packs — the mockup's two-icon toggle.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<GridDensity>))]
public enum GridDensity
{
    /// <summary>Fewer, larger cards. The default the mockup shows selected.</summary>
    Comfortable,

    /// <summary>More, smaller cards — the whole shelf at a glance.</summary>
    Compact,
}

/// <summary>
/// The library's sort order.
/// </summary>
// Sorting happens on the client over the already-loaded cards: the library is one
// user's few hundred series in a single response, so re-sorting is free and a
// round trip per sort choice would be waste.
[JsonConverter(typeof(StrictStringEnumConverter<LibrarySort>))]
public enum LibrarySort
{
    /// <summary>Most to buy plus most in the backlog first — the shelf's own default.</summary>
    MostBehind,

    Alphabetical,

    /// <summary>Most recently refreshed from a metadata source.</summary>
    RecentlyUpdated,

    /// <summary>Most recently read.</summary>
    LastRead,
}
