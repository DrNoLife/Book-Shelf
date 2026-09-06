using LnShelf.Shared.Preferences;

namespace LnShelf.Shared.Shelf;

/// <summary>
/// Sets one series' volume view, or clears the override.
/// </summary>
// Null is a value here, not an omission: it is how "follow the global preference"
// is expressed, so this request has no partial-update semantics.
public sealed record SetSeriesVolumeViewRequest
{
    /// <summary>Null clears the override and the series follows the global view.</summary>
    public VolumeView? View { get; init; }
}

/// <summary>
/// Sets the series' own user state — how it is being tracked, and the note on it
/// (feature-spec §6).
/// </summary>
// Partial update, unlike the request above it: every null leaves that field alone, so
// the status dropdown does not have to send the note back just to avoid clobbering it.
// The same rule UpdatePreferencesRequest follows, and for the same reason — two controls
// on one page, each saving only what it changed.
public sealed record UpdateSeriesStateRequest
{
    /// <summary>Null leaves the tracking status alone.</summary>
    public TrackingStatus? TrackingStatus { get; init; }

    /// <summary>
    /// Null leaves the note alone; an empty string clears it.
    /// </summary>
    // The one thing partial-update semantics cost: with null meaning "unchanged", the
    // erase has to be expressible some other way. A blank note already reads as no note
    // on the way out, so the empty string is that other way and nothing has to know a
    // special sentinel.
    public string? Notes { get; init; }
}
