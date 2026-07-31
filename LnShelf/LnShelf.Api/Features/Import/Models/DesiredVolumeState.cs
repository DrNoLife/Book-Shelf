namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// What the import wants one volume's state to be, after merging every row that
/// refers to it.
/// </summary>
// Merging is needed because one series can arrive under two titles: a real export
// lists Slime volume 15 twice, once as "That Time I Got Reincarnated as a Slime,
// Volume 15" and once as "転生したらスライムだった件 15".
public sealed record DesiredVolumeState
{
    public required int VolumeNumber { get; init; }
    public required ReadStatus ReadStatus { get; init; }
    public required bool Owned { get; init; }
    public OwnedFormat? OwnedFormat { get; init; }
    public DateOnly? ReadAt { get; init; }

    /// <summary>
    /// The ISBN of the edition the export named, normalised. Null when the export
    /// gave none — which is how three of the real file's 76 rows arrive.
    /// </summary>
    // Carried through the merge rather than read straight off the row, because the
    // merge is where two editions of one volume meet and one of the two ISBNs has to
    // win.
    public string? Isbn { get; init; }
}
