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

        return new PreferencesResponse
        {
            GridDensity = preferences.GridDensity,
            LibrarySort = preferences.LibrarySort,
            LibraryView = preferences.LibraryView,
            VolumeView = preferences.VolumeView,
            ShowVolumeTitles = preferences.ShowVolumeTitles,
            DefaultLibraryFilter = preferences.DefaultLibraryFilter,
        };
    }
}
