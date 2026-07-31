namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// Per-series user state. Local Postgres owns this; sync never writes it.
/// </summary>
public sealed class SeriesUserState
{
    /// <summary>Primary key and foreign key — one row per series.</summary>
    public Guid SeriesId { get; set; }

    public TrackingStatus TrackingStatus { get; set; }
    public string? Notes { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? LastReadAt { get; set; }

    public TitleLanguage? TitleLanguageOverride { get; set; }
    public CoverSource? CoverSourceOverride { get; set; }

    /// <summary>
    /// This series' volume view. Null means "follow the global preference" — the same
    /// shape as the two override columns above it.
    /// </summary>
    public VolumeView? VolumeViewOverride { get; set; }

    public bool NotificationsMuted { get; set; }

    // The one sanctioned stored non-derived flag (CLAUDE.md): last-visit
    // acknowledgment is an event, not something derivable from volume state.
    // Set by sync when an unseen released volume appears, cleared by the user.
    public bool HasNewVolume { get; set; }
}
