namespace LnShelf.Shared.Shelf;

/// <summary>
/// One of the two independent per-volume user axes; the other is ownership.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ReadStatus>))]
public enum ReadStatus
{
    Unread,
    Reading,
    Read,
}
