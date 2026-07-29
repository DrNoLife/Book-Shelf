namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// A child of a series and the unit of user state. Volumes arrive from metadata
/// sync; <see cref="IsLocalOnly"/> marks the manual escape hatch (§4.1).
/// </summary>
public sealed class Volume
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }

    public int Number { get; set; }

    /// <summary>Null means the date is unknown — announced, or simply unrecorded.</summary>
    public DateOnly? ReleaseDate { get; set; }

    /// <summary>
    /// The release source says this volume is published, without saying when.
    /// </summary>
    // Source metadata, not a derived state, so it does not fall under CLAUDE.md's
    // "derived states are computed, never stored" rule — it sits beside ReleaseDate
    // for the same reason ReleaseDate does. Written justification, as that rule asks
    // for a stored flag:
    //
    // MangaUpdates has no per-volume dates (its /releases feed is scanlation
    // chapters; 187 real release records across four of this shelf's series contained
    // zero publisher volumes). What it does state is a count: "16 Volumes (Ongoing)",
    // and per publisher "Yen On — 16 Volumes; Ongoing". That is a fact about how many
    // volumes exist, and it is the only honest way to know that volume 16 is out and
    // unbought while volume 17 is not. A date cannot be derived from it, and
    // inventing one to fit the existing column would be fabricating data.
    public bool IsReleased { get; set; }

    public string? CoverObjectKey { get; set; }

    /// <summary>Null exactly when <see cref="IsLocalOnly"/> is true.</summary>
    public string? ExternalId { get; set; }

    public bool IsLocalOnly { get; set; }

    public VolumeUserState? UserState { get; set; }
}
