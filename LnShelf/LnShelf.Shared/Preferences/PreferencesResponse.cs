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

    /// <summary>Covers grid or rows — the library's two views of the same set.</summary>
    public required LibraryView LibraryView { get; init; }

    /// <summary>
    /// The series-detail default. A series may override it
    /// (<see cref="LnShelf.Shared.Shelf.SeriesDetailResponse.VolumeViewOverride"/>).
    /// </summary>
    public required VolumeView VolumeView { get; init; }

    /// <summary>
    /// Whether a volume's own title is printed in its caption where one exists. Off by
    /// default — most volumes are named "Volume N", which the caption already says.
    /// </summary>
    public required bool ShowVolumeTitles { get; init; }

    /// <summary>The first of <see cref="DefaultLibraryFilters"/>, as the single value 0.30.0 sent.</summary>
    // Still sent for one release so a browser holding the 0.30 bundle, which declares this
    // property required, can read the response. Remove in the release after 0.31.0.
    public required LibraryFilter DefaultLibraryFilter { get; init; }

    /// <summary>
    /// The status filters the shelf opens on when this browser has none of its own
    /// remembered. Normalised (<see cref="LibraryFilterSet.Normalize"/>); Behind by default.
    /// </summary>
    public required IReadOnlyList<LibraryFilter> DefaultLibraryFilters { get; init; }

    /// <summary>
    /// The library page's sections in the reader's order, each exactly once
    /// (<see cref="LibrarySections.Normalize"/>).
    /// </summary>
    public required IReadOnlyList<LibrarySection> LibrarySectionOrder { get; init; }
}

/// <summary>Partial update: every null leaves that setting alone.</summary>
public sealed record UpdatePreferencesRequest
{
    public GridDensity? GridDensity { get; init; }
    public LibrarySort? LibrarySort { get; init; }
    public LibraryView? LibraryView { get; init; }
    public VolumeView? VolumeView { get; init; }
    public bool? ShowVolumeTitles { get; init; }

    /// <summary>Normalised on save, so an empty set stores All.</summary>
    public IReadOnlyList<LibraryFilter>? DefaultLibraryFilters { get; init; }

    /// <summary>Normalised on save: repeats dropped, missing sections appended.</summary>
    public IReadOnlyList<LibrarySection>? LibrarySectionOrder { get; init; }
}
