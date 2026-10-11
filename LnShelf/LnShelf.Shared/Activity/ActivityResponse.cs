using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Activity;

/// <summary>
/// "Since your last visit": what the sync noticed on the shelf since the reader last
/// dismissed the panel, grouped per series, the series with the newest news first.
/// </summary>
public sealed record ActivityResponse
{
    /// <summary>Empty when there is nothing to tell; the library then shows no panel.</summary>
    public required IReadOnlyList<ActivitySeriesResponse> Series { get; init; }

    /// <summary>
    /// When the newest event this response read happened; null when it read none. "Got it"
    /// sends it back, so an event the sync writes after this load is not acknowledged unseen.
    /// </summary>
    public DateTimeOffset? Through { get; init; }
}

/// <summary>One series and what happened to its volumes, newest first.</summary>
public sealed record ActivitySeriesResponse
{
    public required Guid SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required Format Format { get; init; }

    /// <summary>Never empty.</summary>
    public required IReadOnlyList<ActivityEventResponse> Events { get; init; }
}

/// <summary>One thing that happened to one volume.</summary>
public sealed record ActivityEventResponse
{
    public required ShelfEventKind Kind { get; init; }
    public required decimal VolumeNumber { get; init; }

    /// <summary>
    /// The date before a <see cref="ShelfEventKind.ReleaseDateChanged"/>. Null on that kind
    /// means the volume was dated for the first time, not that it moved.
    /// </summary>
    public DateOnly? PreviousDate { get; init; }

    /// <summary>The volume's date after the event, when it has one.</summary>
    public DateOnly? NewDate { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
