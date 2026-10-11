namespace LnShelf.Shared.Shelf;

/// <summary>
/// What a sync noticed about one volume of a series that had been synced before: the kinds
/// of entry in the shelf's activity log, which feeds both push notifications and "Since your
/// last visit".
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ShelfEventKind>))]
public enum ShelfEventKind
{
    /// <summary>A volume that was not out at the previous sync is out now.</summary>
    VolumeReleased,

    /// <summary>A volume the shelf did not know of appeared, and is not out yet.</summary>
    VolumeAnnounced,

    /// <summary>
    /// A volume that is not out yet got a new future date. A null previous date means it
    /// was dated for the first time ("Vol 18 dated 12 Dec"), not that it moved.
    /// </summary>
    ReleaseDateChanged,
}
