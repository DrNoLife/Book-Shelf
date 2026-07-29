namespace LnShelf.Shared.Shelf;

/// <summary>
/// How an owned volume is held. Null when the volume is not owned.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OwnedFormat>))]
public enum OwnedFormat
{
    Physical,
    Digital,
}
