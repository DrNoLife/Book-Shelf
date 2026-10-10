namespace LnShelf.Shared.Shelf;

/// <summary>
/// Everything one library card renders, delivered in a single row per series
/// (feature-spec §4.1 — the grid must not round-trip per series).
/// </summary>
public sealed record LibraryCardResponse
{
    public required Guid SeriesId { get; init; }
    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public required TrackingStatus TrackingStatus { get; init; }
    public required bool HasNewVolume { get; init; }

    /// <summary>
    /// Key for <c>/api/covers/{objectKey}</c>. Null renders the placeholder; the
    /// client never builds a URL to an external CDN (design.md).
    /// </summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>Volumes marked read.</summary>
    public required int Read { get; init; }

    /// <summary>Volumes owned, on either ownership format.</summary>
    public required int Owned { get; init; }

    /// <summary>Volumes released on or before today.</summary>
    public required int Published { get; init; }

    /// <summary>Released and not owned.</summary>
    public required int ToBuy { get; init; }

    /// <summary>Owned and not read.</summary>
    public required int Backlog { get; init; }

    /// <summary>Volumes known to the shelf, including unreleased and announced ones.</summary>
    public required int Tracked { get; init; }

    /// <summary>Lowest to-buy volume number — feeds the "Vol 12 on Saxo" CTA. Null when caught up.</summary>
    public int? NextToBuyNumber { get; init; }

    /// <summary>
    /// The lowest-numbered volume that is owned and not read — what "Continue reading" offers
    /// to mark finished. Null when nothing on the shelf is waiting.
    /// </summary>
    // The id rides along with the number so the library can mark the volume read in one
    // click; without it the card could only link to the series page and leave the work there.
    public Guid? NextToReadVolumeId { get; init; }

    /// <summary>The number of <see cref="NextToReadVolumeId"/>'s volume. Null when nothing is waiting.</summary>
    public int? NextToReadNumber { get; init; }

    /// <summary>
    /// The earliest date a volume that is not out yet carries. Null when no source has
    /// dated anything — which is currently every series (backlog: the enrichment spike).
    /// </summary>
    public DateOnly? NextReleaseDate { get; init; }

    /// <summary>
    /// One entry per volume, in number order — what the rows view's tick strip draws.
    /// </summary>
    // Carried on the card rather than fetched per series: the rows view shows the whole
    // shelf at once, so a round trip per row is exactly what feature-spec §4.1 forbids.
    public IReadOnlyList<VolumeTickResponse> VolumeTicks { get; init; } = [];

    /// <summary>
    /// Carried so the client can sort by them without a second round trip.
    /// </summary>
    // The library is one response holding every card, so sorting is a client
    // concern; these are the two orderings that need data the counts do not have.
    public DateOnly? LastReadAt { get; init; }

    public DateTimeOffset? LastSyncedAt { get; init; }

    // Derived from the counts, never stored (CLAUDE.md). Computed here rather
    // than in SQL so the projection doesn't repeat both count subqueries a
    // third time; it is still nowhere near the database.
    public bool Behind => ToBuy > 0 || Backlog > 0;
}
