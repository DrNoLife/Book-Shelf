namespace LnShelf.Api.Features.Preferences.Models;

/// <summary>
/// One user's preferences row (feature-spec §4.6).
/// </summary>
// Keyed by its owner rather than by an id of its own: one user, one row, and a table that
// could hold two rows for one user would invite code that has to choose between them.
public sealed class UserPreferences : IOwned
{
    /// <summary>The user these belong to, and the primary key (IOwned).</summary>
    public Guid OwnerId { get; set; }

    public GridDensity GridDensity { get; set; } = GridDensity.Comfortable;

    public LibrarySort LibrarySort { get; set; } = LibrarySort.MostBehind;

    /// <summary>Covers is the default — the design's thesis is covers doing the talking.</summary>
    public LibraryView LibraryView { get; set; } = LibraryView.Covers;

    /// <summary>The series-detail default; a series may override it.</summary>
    public VolumeView VolumeView { get; set; } = VolumeView.Tiles;

    /// <summary>
    /// Whether a volume's own title is printed in its caption where one exists.
    /// </summary>
    // On by default since the catalogue pass: titles used to be a promise the shelf could
    // not keep (no source named a volume), so the default hid an empty feature. They are
    // now real data on the large majority of the shelf, and a default that hides data the
    // app went and fetched serves nobody. The toggle stays for the reader who finds
    // "Volume 3" after "Vol 3" redundant, and a series with no titles still shows none.
    public bool ShowVolumeTitles { get; set; } = true;

    /// <summary>
    /// The status filters the shelf opens on when the browser has none remembered. Behind is
    /// the opinionated default (§4.6).
    /// </summary>
    public IReadOnlyList<LibraryFilter> DefaultLibraryFilters { get; set; } = LibraryFilterSet.Default;

    /// <summary>The library page's sections, in the order the reader wants them.</summary>
    public IReadOnlyList<LibrarySection> LibrarySectionOrder { get; set; } = LibrarySections.Default;

    /// <summary>
    /// When the reader last dismissed "Since your last visit". Null means never: the panel
    /// then looks back a fixed window instead.
    /// </summary>
    // An acknowledgment, like SeriesUserState.HasNewVolume: an event the reader caused, not
    // something derivable from the shelf.
    public DateTimeOffset? ActivitySeenAt { get; set; }
}
