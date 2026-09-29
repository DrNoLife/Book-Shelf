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

    /// <summary>
    /// When the volume was finished — the latest date on a row that says Read. Null when
    /// no Read row carried one.
    /// </summary>
    public DateOnly? ReadAt { get; init; }

    /// <summary>
    /// The earliest date the export gives for this volume having been read at all: any
    /// dated row that says Read or Reading. Feeds the series' start date only.
    /// </summary>
    // Its own field rather than ReadAt, because the two answer different questions. Both
    // exports' date column is a *finish* date ("Last Date Read", "Date Read"), so on a
    // currently-reading row it is an earlier completed read — not evidence the volume is
    // read now, which is why it cannot be ReadAt, but proof the reading had started by
    // then, which is exactly what StartedAt asks. A did-not-finish row maps to Unread and
    // stays out of both: an abandoned attempt is not the reading the series tracks.
    public DateOnly? ReadingEvidenceAt { get; init; }

    /// <summary>
    /// The volume's own subtitle as the export spelled it, or null when no row for
    /// this volume carried one.
    /// </summary>
    public string? Subtitle { get; init; }

    /// <summary>
    /// The ISBN of the edition the export named, normalised. Null when the export
    /// gave none — which is how three of the real file's 76 rows arrive.
    /// </summary>
    // Carried through the merge rather than read straight off the row, because the
    // merge is where two editions of one volume meet and one of the two ISBNs has to
    // win.
    public string? Isbn { get; init; }
}
