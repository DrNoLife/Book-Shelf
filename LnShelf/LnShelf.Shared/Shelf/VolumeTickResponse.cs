namespace LnShelf.Shared.Shelf;

/// <summary>
/// One volume as the library's tick strip draws it: the raw facts, plus the one
/// appearance they add up to.
/// </summary>
// Deliberately four facts and no cover key or price: a rows view needs one tick per
// volume across the whole shelf, and the card query is the hot path (data-access.md).
public sealed record VolumeTickResponse
{
    public required int Number { get; init; }

    /// <summary>Decided by the server, on the same three grounds as the card's Published.</summary>
    public required bool IsOut { get; init; }

    public required bool Owned { get; init; }

    public required ReadStatus ReadStatus { get; init; }

    /// <summary>
    /// The tick's appearance — derived, never stored, and computed here so the server
    /// and the strip cannot disagree about it.
    /// </summary>
    // The four tick kinds are a coarsening of the volume tile's six states: a tile
    // distinguishes Reading from Owned-unread and dated from announced, a 4px tick
    // cannot, so Reading collapses into whichever of owned-unread / to-buy it is, and
    // both flavours of "not out" collapse into one hollow tick.
    public VolumeTickKind Kind => (IsOut, ReadStatus, Owned) switch
    {
        (false, _, _) => VolumeTickKind.NotOut,
        (true, ReadStatus.Read, _) => VolumeTickKind.Read,
        (true, _, true) => VolumeTickKind.OwnedUnread,
        _ => VolumeTickKind.ToBuy,
    };
}

/// <summary>The four appearances a tick can carry.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<VolumeTickKind>))]
public enum VolumeTickKind
{
    Read,
    OwnedUnread,
    ToBuy,

    /// <summary>Not out yet, dated or not — the hollow tick.</summary>
    NotOut,
}
