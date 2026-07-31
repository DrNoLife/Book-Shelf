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

    /// <summary>
    /// The ISBN of the edition this volume stands for, when one is known. Null is
    /// the norm — most volumes reach the shelf from a volume count.
    /// </summary>
    // Source metadata, not a derived state, so it sits beside ReleaseDate and
    // IsReleased rather than under the "computed, never stored" rule. The written
    // justification feature-spec §5 asks of a stored field:
    //
    // It is the only key that per-volume enrichment works by. Open Library answers
    // GET /isbn/{isbn}.json with the one correct English edition; the same catalogue
    // searched by title answers with Japanese editions and omnibuses (measured:
    // .research/volume-enrichment/e-isbn-first-pipeline.md — 63% strict pass by ISBN,
    // 0 of 7 clean resolutions by title). It cannot be derived from series + number,
    // which is exactly why it has to be kept: it arrives once, from a StoryGraph
    // export or a discovery pass, and every later lookup depends on still having it.
    //
    // Distinct from ExternalId on purpose. That column is AniList's own volume
    // identity; putting a second source's identifier in it would mean two sources
    // sharing one field and neither being able to say which id it holds.
    public string? Isbn { get; set; }

    /// <summary>
    /// This volume's own title, when a source supplies one. Null when nothing has
    /// enriched it, which is most volumes.
    /// </summary>
    // Plain synced metadata, the same class of fact as ReleaseDate, so it needs no
    // special justification under the "derived states are computed, never stored" rule:
    // it is Open Library's full_title for the edition behind Isbn, and there is nothing
    // to derive it from.
    //
    // Expected to stay null far more often than not, and that is the design rather than
    // a shortfall — light-novel lines name their volumes "Volume N", which is the number
    // the caption already prints. The tile's Title parameter was built optional for
    // exactly this reason and invents nothing when it is absent.
    public string? Title { get; set; }

    public string? CoverObjectKey { get; set; }

    /// <summary>Null exactly when <see cref="IsLocalOnly"/> is true.</summary>
    public string? ExternalId { get; set; }

    public bool IsLocalOnly { get; set; }

    public VolumeUserState? UserState { get; set; }
}
