namespace LnShelf.Shared.Preferences;

/// <summary>
/// How the library renders its series: as a cover grid or as rows.
/// </summary>
// Two views of the same set, not two filters: every chip, tab, sort and
// type-to-filter applies identically in both. Covers is the default because the
// design's thesis is "covers doing the talking"; rows exist for the days the
// question is "what am I behind on", where a scannable table beats artwork.
[JsonConverter(typeof(JsonStringEnumConverter<LibraryView>))]
public enum LibraryView
{
    Covers,
    Rows,
}
