namespace LnShelf.Shared.Shelf;

/// <summary>
/// The medium a series is published in — a first-class dimension, never a tag.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<Format>))]
public enum Format
{
    LightNovel,
    Manga,
    WebNovel,
}
