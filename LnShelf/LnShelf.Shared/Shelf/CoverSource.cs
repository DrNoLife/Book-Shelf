namespace LnShelf.Shared.Shelf;

/// <summary>
/// Which volume's cover represents the series on a card.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CoverSource>))]
public enum CoverSource
{
    LatestVolume,
    FirstVolume,
}
