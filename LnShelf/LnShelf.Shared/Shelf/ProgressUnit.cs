namespace LnShelf.Shared.Shelf;

/// <summary>
/// What a series' reading progress is counted in, chosen per series by the reader: Volumes for
/// every series unless the reader picks Chapters, which only a manga or a web novel offers.
/// </summary>
// Volumes first, so it is the enum's default: every series that existed before the choice did
// is a Volumes series, and a row or a payload that never mentions the unit reads the same.
[JsonConverter(typeof(StrictStringEnumConverter<ProgressUnit>))]
public enum ProgressUnit
{
    Volumes,
    Chapters,
}
