namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// Per-series user state. Local Postgres owns this; sync never writes it.
/// </summary>
public sealed class SeriesUserState
{
    /// <summary>Primary key and foreign key — one row per series.</summary>
    public Guid SeriesId { get; set; }

    /// <summary>The owning series. The owner filter reads it.</summary>
    public Series? Series { get; set; }

    /// <summary>The notes column's width, shared by its configuration and the write guard.</summary>
    // One constant for both, so a note the service accepts is always a note the column
    // can hold — a longer one used to reach Postgres and come back as a 500.
    public const int NotesMaximumLength = 4000;

    public TrackingStatus TrackingStatus { get; set; }
    public string? Notes { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? LastReadAt { get; set; }

    /// <summary>
    /// This series' volume view. Null means "follow the global preference".
    /// </summary>
    public VolumeView? VolumeViewOverride { get; set; }

    public bool NotificationsMuted { get; set; }

    // The one sanctioned stored non-derived flag (CLAUDE.md): last-visit
    // acknowledgment is an event, not something derivable from volume state.
    // Set by sync when an unseen released volume appears, cleared by the user.
    public bool HasNewVolume { get; set; }

    /// <summary>
    /// The row a series gets the first time anything about it is recorded.
    /// </summary>
    // PlanToStart spelled out, because the enum's default is Reading and every projection
    // reads a *missing* row as PlanToStart. Creating the row for an unrelated reason —
    // a note, a volume view, a read tick — must not silently move the series to Reading.
    public static SeriesUserState Untouched(Guid seriesId) => new()
    {
        SeriesId = seriesId,
        TrackingStatus = TrackingStatus.PlanToStart,
    };
}
