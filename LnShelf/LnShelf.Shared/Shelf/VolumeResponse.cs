namespace LnShelf.Shared.Shelf;

/// <summary>
/// One volume, as the grid renders it.
/// </summary>
public sealed record VolumeResponse
{
    public required Guid VolumeId { get; init; }
    public required int Number { get; init; }

    /// <summary>Null when no source knows the date, which is most volumes.</summary>
    public DateOnly? ReleaseDate { get; init; }

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

    /// <summary>Added by hand or by an import, not by a metadata source.</summary>
    public required bool IsLocalOnly { get; init; }
}
