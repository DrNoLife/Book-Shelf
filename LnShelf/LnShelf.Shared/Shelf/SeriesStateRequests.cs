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
