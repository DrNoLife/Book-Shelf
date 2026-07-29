using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Preferences;

/// <summary>
/// The single preferences row (feature-spec §4.6), exposed as one GET/PUT.
/// </summary>
// Only the settings something actually reads are here. The rest of §4.6's list —
// title language, cover source, sync hour, notification config — arrives with the
// features that consume them rather than as dead columns.
public sealed record PreferencesResponse
{
    public required GridDensity GridDensity { get; init; }
    public required LibrarySort LibrarySort { get; init; }

    /// <summary>The library's opinionated default filter (§4.6).</summary>
    public required LibraryFilter DefaultLibraryFilter { get; init; }
}

/// <summary>Partial update: every null leaves that setting alone.</summary>
public sealed record UpdatePreferencesRequest
{
    public GridDensity? GridDensity { get; init; }
    public LibrarySort? LibrarySort { get; init; }
    public LibraryFilter? DefaultLibraryFilter { get; init; }
}
