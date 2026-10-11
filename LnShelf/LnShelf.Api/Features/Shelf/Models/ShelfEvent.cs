namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// One thing a sync noticed about a series that had been synced before: a volume came out,
/// was announced, or got a new date. The activity log behind push notifications and
/// "Since your last visit".
/// </summary>
// A stored event log, which CLAUDE.md asks a written justification for, the same kind
// HasNewVolume has: "Vol 14 came out last night" is an event, a change between two syncs,
// and the volume rows only hold the state after it. Once the next sync has run, nothing on
// the shelf can say what changed. Pruned after 120 days by the nightly job.
//
// A child of Series rather than an IOwned root: it means nothing without its series, dies
// with it by cascade, and is filtered through it like Volume.
public sealed class ShelfEvent
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }

    /// <summary>The owning series. The owner filter reads it.</summary>
    public Series? Series { get; set; }

    public ShelfEventKind Kind { get; set; }

    /// <summary>
    /// The volume's number, not its id: a number is what the message prints, and it stays
    /// meaningful after a volume merge deletes the row it was about.
    /// </summary>
    public decimal VolumeNumber { get; set; }

    /// <summary>The date before a <see cref="ShelfEventKind.ReleaseDateChanged"/>. Null when it had none.</summary>
    public DateOnly? PreviousDate { get; set; }

    /// <summary>The volume's date after the change, when it has one.</summary>
    public DateOnly? NewDate { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// When the notification pass dealt with this event: delivered, muted, stale or no
    /// device to send to. Null means not yet looked at, or every device failed transiently.
    /// </summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
