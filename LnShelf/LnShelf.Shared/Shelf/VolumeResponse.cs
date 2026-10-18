using LnShelf.Shared.Metadata;

namespace LnShelf.Shared.Shelf;

/// <summary>
/// One volume, as the grid renders it.
/// </summary>
public sealed record VolumeResponse
{
    public required Guid VolumeId { get; init; }
    public required decimal Number { get; init; }

    /// <summary>Null when no source knows the date, which is most volumes.</summary>
    public DateOnly? ReleaseDate { get; init; }

    /// <summary>
    /// Which per-volume source wrote <see cref="ReleaseDate"/> ("Date from Open Library"). Null
    /// when the date is unknown or was written before its source was recorded.
    /// </summary>
    public MetadataSourceName? ReleaseDateSource { get; init; }

    /// <summary>
    /// This volume's own title, when a source supplied one. Null is the norm, and
    /// renders as nothing at all rather than as a placeholder.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// This volume's own blurb, when a source supplied one. Null is a normal answer and
    /// renders as nothing at all rather than as a placeholder.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this volume is out, decided by the server so the client never has to
    /// re-derive it: a date in the past, or the release source's word, or ownership.
    /// </summary>
    // The same rule as LibraryCardProjection's Published, so a tile and the card
    // above it can never disagree about what is out.
    public required bool IsOut { get; init; }

    public required ReadStatus ReadStatus { get; init; }
    public required bool Owned { get; init; }
    public OwnedFormat? OwnedFormat { get; init; }
    public DateOnly? ReadAt { get; init; }

    public decimal? PurchasePrice { get; init; }

    /// <summary>
    /// The cover to render: the user's override when there is one, otherwise
    /// whatever a source supplied.
    /// </summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>
    /// True when <see cref="CoverObjectKey"/> is a user-supplied override, so the UI
    /// can offer to remove it and say that sync will not touch it.
    /// </summary>
    public bool HasCoverOverride { get; init; }

    /// <summary>
    /// The cover to try when <see cref="CoverObjectKey"/> cannot load: what it would be without
    /// the user's override (this volume's synced art, else the series'). Null when there is no
    /// override, or nothing behind it.
    /// </summary>
    // Optional with a null default so a client bundle from before it existed still reads the
    // response. An override's file can go missing from the store while the row keeps its key,
    // and sync may never touch the row (CLAUDE.md), so the client is told what to fall back to.
    public string? CoverFallbackObjectKey { get; init; }

    /// <summary>Added by hand or by an import, not by a metadata source.</summary>
    public required bool IsLocalOnly { get; init; }

    /// <summary>The first chapter collected in this volume. Null when no chapter map covers it.</summary>
    // Optional like CoverFallbackObjectKey, so a bundle from before chapter ranges still reads it.
    public decimal? FirstChapter { get; init; }

    /// <summary>
    /// The last chapter collected in this volume. Null while it is open — the next volume has no
    /// known start yet — which reads as "Ch. 138–".
    /// </summary>
    public decimal? LastChapter { get; init; }

    /// <summary>Which source the chapter range came from, for the credit line. Null without a range.</summary>
    public MetadataSourceName? ChapterRangeSource { get; init; }
}
