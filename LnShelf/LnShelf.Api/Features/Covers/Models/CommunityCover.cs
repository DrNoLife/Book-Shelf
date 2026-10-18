namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// One consenting user's published pointer to their own uploaded cover for a catalogued
/// volume (feature-spec §4.3).
/// </summary>
// Deliberately not IOwned and not owner-filtered: other users have to read it by match key.
// It holds no bytes, only the key of the sharer's own override object and a hash of what is
// stored there; a user who picks the cover gets a copy under their own key. Every write is
// scoped by hand to the current user (CommunityCoverCommandService), and it is deleted with
// the sharer, the volume or the series.
public sealed class CommunityCover
{
    public Guid Id { get; set; }

    public Guid UploaderUserId { get; set; }

    /// <summary>The sharer's own volume; unique, so a volume publishes at most one cover.</summary>
    public Guid SourceVolumeId { get; set; }

    // The series' ids as they were when the row was last staged, so a reader can match without
    // reading another user's series.
    public int? AniListId { get; set; }

    public long? MangaUpdatesId { get; set; }

    public Format Format { get; set; }

    public decimal Number { get; set; }

    /// <summary>Lowercase hex SHA-256 of the stored bytes.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The sharer's override object.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; set; }
}
