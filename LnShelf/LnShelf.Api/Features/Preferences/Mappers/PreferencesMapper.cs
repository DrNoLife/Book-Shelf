namespace LnShelf.Api.Features.Preferences.Mappers;

/// <summary>
/// The preferences row → the wire. Hand-written, explicit, no reflection
/// (CLAUDE.md — Mapping).
/// </summary>
public static class PreferencesMapper
{
    public static PreferencesResponse ToResponse(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        // Normalised on the way out as well as on the way in: the column reader drops a
        // name the enum no longer has, and what is left must still be a whole answer.
        var defaultFilters = LibraryFilterSet.Normalize(preferences.DefaultLibraryFilters);

        return new PreferencesResponse
        {
            GridDensity = preferences.GridDensity,
            LibrarySort = preferences.LibrarySort,
            LibraryView = preferences.LibraryView,
            VolumeView = preferences.VolumeView,
            ShowVolumeTitles = preferences.ShowVolumeTitles,
            DefaultLibraryFilters = defaultFilters,

            // Normalize never returns an empty set, so there is always a first entry.
            DefaultLibraryFilter = defaultFilters[0],
            LibrarySectionOrder = LibrarySections.Normalize(preferences.LibrarySectionOrder),
        };
    }
}
