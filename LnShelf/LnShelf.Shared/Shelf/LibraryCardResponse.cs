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

    // Derived from the counts, never stored (CLAUDE.md). Computed here rather
    // than in SQL so the projection doesn't repeat both count subqueries a
    // third time; it is still nowhere near the database.
    public bool Behind => ToBuy > 0 || Backlog > 0;
}
