namespace LnShelf.Shared.Preferences;

/// <summary>
/// How the series-detail page renders its volumes: as tiles or as a list.
/// </summary>
// Held both globally (the preferences row) and per series
// (SeriesUserState.VolumeViewOverride, null meaning "follow the global"), because
// the right answer differs by series: a 42-volume manga is a list, a 14-volume
// light novel with cover art is tiles.
[JsonConverter(typeof(StrictStringEnumConverter<VolumeView>))]
public enum VolumeView
{
    Tiles,
    List,
}
