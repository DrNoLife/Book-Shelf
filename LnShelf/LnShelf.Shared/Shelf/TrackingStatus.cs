namespace LnShelf.Shared.Shelf;

/// <summary>
/// How the user is tracking a series. User state — never written by sync.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<TrackingStatus>))]
public enum TrackingStatus
{
    Reading,
    PlanToStart,
    Completed,
    Dropped,
}
