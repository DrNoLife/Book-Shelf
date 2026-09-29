namespace LnShelf.Shared.Shelf;

/// <summary>
/// Publication state of the series itself. Comes from metadata, not the user —
/// distinct from <see cref="TrackingStatus"/>.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<PublicationStatus>))]
public enum PublicationStatus
{
    Ongoing,
    Completed,
    Hiatus,
    Cancelled,
}
