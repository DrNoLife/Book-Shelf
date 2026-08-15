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
    /// This volume's own subtitle — the part of a source's title that is not the series
    /// and not the number. Three states, and the difference between the last two matters:
    /// <list type="bullet">
    /// <item><c>null</c> — no source has named this volume yet.</item>
    /// <item><c>""</c> — a source named it and it has no subtitle of its own.</item>
    /// <item>anything else — the subtitle, e.g. "Run Through the Battlefront (Finish)".</item>
    /// </list>
    /// </summary>
    // Plain synced metadata, the same class of fact as ReleaseDate, so it needs no
    // special justification under the "derived states are computed, never stored" rule:
    // it is what a store's or a library's own title for the edition says beyond what the
    // shelf already knows, and there is nothing to derive it from.
    //
    // The empty string is a stored answer rather than a blank. Whole series legitimately
    // have no subtitles — every volume of That Time I Got Reincarnated as a Slime is titled
    // only "…, Vol. N" — and without a way to record "asked, and the answer was none",
    // fill-if-null enrichment would re-ask about those volumes on every run forever.
    // Enrichment therefore treats "" as resolved, and SeriesDetailProjection maps it back
    // to null on the way out, so the wire contract and the client keep seeing exactly two
    // states: a subtitle, or nothing to print.
    //
    // Expected to be empty far more often than not, and that is the design rather than a
    // shortfall — the caption already prints the number. The tile's Title parameter was
    // built optional for exactly this reason and invents nothing when it is absent.
    public string? Title { get; set; }

    /// <summary>
    /// This volume's own blurb, when a source supplies one. Null when nothing has
    /// enriched it.
    /// </summary>
    // Plain synced metadata, the same class of fact as ReleaseDate and Title, so it needs
    // no special justification under the "derived states are computed, never stored" rule:
    // it is a store's own copy for this volume, and there is nothing to derive it from.
    //
    // Stored rather than fetched on read, for the reason the series synopsis is: the shelf
    // must render without the source being up, and a detail page that made a live store
    // request per volume would be both slow and rude.
    //
    // Uncapped, unlike Title — the same call SeriesConfiguration makes about
    // Series.Description. A blurb is prose whose length is the source's business, and
    // Postgres `text` costs nothing over varchar(n) while being unable to truncate one
    // mid-sentence.
    public string? Description { get; set; }

    public string? CoverObjectKey { get; set; }

    /// <summary>Null exactly when <see cref="IsLocalOnly"/> is true.</summary>
    public string? ExternalId { get; set; }

    public bool IsLocalOnly { get; set; }

    public VolumeUserState? UserState { get; set; }
}
