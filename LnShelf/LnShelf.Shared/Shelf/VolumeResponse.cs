namespace LnShelf.Shared.Shelf;

/// <summary>
/// One volume, as the grid renders it.
/// </summary>
public sealed record VolumeResponse
{
    public required Guid VolumeId { get; init; }
    public required int Number { get; init; }

    /// <summary>Null when no source knows the date — which is currently all of them.</summary>
    public DateOnly? ReleaseDate { get; init; }

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

    public string? CoverObjectKey { get; init; }

    /// <summary>Added by hand or by an import, not by a metadata source.</summary>
    public required bool IsLocalOnly { get; init; }
}
