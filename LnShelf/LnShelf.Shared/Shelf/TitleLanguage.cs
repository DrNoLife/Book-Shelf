namespace LnShelf.Shared.Shelf;

/// <summary>
/// Which title variant to display. Global preference, overridable per series.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TitleLanguage>))]
public enum TitleLanguage
{
    English,
    Romaji,
    Native,
}
