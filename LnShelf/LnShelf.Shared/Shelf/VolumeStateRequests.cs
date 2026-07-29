namespace LnShelf.Shared.Shelf;

/// <summary>
/// Sets one volume's read status (feature-spec §6).
/// </summary>
// An explicit status rather than a "cycle" verb: the endpoint is then idempotent and
// testable, and the Unread → Reading → Read → Unread cycle stays where it belongs,
// in the tile the user is clicking.
public sealed record SetVolumeReadRequest
{
    public required ReadStatus Status { get; init; }
}

/// <summary>Sets one volume's ownership (feature-spec §6).</summary>
public sealed record SetVolumeOwnedRequest
{
    public required bool Owned { get; init; }

    /// <summary>Ignored when <see cref="Owned"/> is false.</summary>
    public OwnedFormat? Format { get; init; }

    public decimal? PurchasePrice { get; init; }
}

/// <summary>
/// "Mark 1–8 read" / "Mark 1–11 owned" — the control an imported collection needs
/// (feature-spec §4.1).
/// </summary>
public sealed record BulkVolumeStateRequest
{
    /// <summary>Volumes 1..N inclusive.</summary>
    public required int ThroughNumber { get; init; }

    /// <summary>Null leaves read status alone.</summary>
    public ReadStatus? Read { get; init; }

    /// <summary>Null leaves ownership alone.</summary>
    public bool? Owned { get; init; }
}
