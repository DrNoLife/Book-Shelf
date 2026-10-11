namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// The trackable entity (feature-spec §5). The light novel and the manga of the
/// same title are two separate series, linked through <see cref="RelatedSeriesId"/>.
/// </summary>
// Persistence models are mutable classes rather than the records CLAUDE.md
// prefers: EF materialisation and change tracking need settable members. They
// never leave this project — queries project to LnShelf.Shared records.
public sealed class Series : IOwned
{
    public Guid Id { get; set; }

    /// <summary>The user this belongs to (IOwned). Stamped on save.</summary>
    public Guid OwnerId { get; set; }

    // Both external ids are nullable: a manual-only series (WebNovel — §4.2) has
    // neither, and Postgres unique indexes permit multiple nulls.
    public int? AniListId { get; set; }

    /// <summary>
    /// A long, not an int: MangaUpdates series ids are eleven digits
    /// (45348350266) and overflow an Int32.
    /// </summary>
    public long? MangaUpdatesId { get; set; }

    public Format Format { get; set; }

    public string TitleEnglish { get; set; } = string.Empty;
    public string? TitleRomaji { get; set; }
    public string? TitleNative { get; set; }

    /// <summary>
    /// Every *other* title a metadata source knows this series by — AniList's romaji, or
    /// the unlabelled bag MangaUpdates keeps in <c>associated</c>.
    /// </summary>
    // Stored because the volume catalogue has to be asked under a name it recognises, and
    // which name that is cannot be worked out from this row: MangaUpdates files a large
    // share of series under romaji ("Kono Subarashii Sekai ni Shukufuku o!") while the US
    // storefront carries only the English name ("Konosuba: God's Blessing on This Wonderful
    // World!"), and it labels none of the alternatives it offers.
    //
    // Filtered to Latin-script titles, deduped and capped where it is written
    // (SeriesKnownTitles) — each entry is a store request the enrichment may spend.
    public string[] AlternativeTitles { get; set; } = [];

    /// <summary>
    /// The title the volume catalogue actually answered to. Null until one has.
    /// </summary>
    // A stored field that is not a derived state, so it needs the justification CLAUDE.md
    // asks for: it is not computed from anything on this row. It is source metadata — the
    // name the store knows this series by — and the same class of fact as
    // <see cref="MangaUpdatesId"/>, which is likewise another catalogue's own handle for
    // the same series, learned once and kept.
    //
    // Kept because arbitration costs requests. The store is what decides which of a series'
    // titles it recognises, and Konosuba's is the fourth of four the shelf can offer; a
    // nightly sweep that did not remember the winner would spend four requests a night
    // re-answering a settled question. Re-learnable by setting it back to null, which the
    // enrichment pass does itself the moment the remembered title stops matching.
    public string? CatalogueTitle { get; set; }

    public PublicationStatus PublicationStatus { get; set; }
    public string? Publisher { get; set; }
    public int? StartYear { get; set; }
    public string[] Genres { get; set; } = [];
    public string? Author { get; set; }
    public string? Illustrator { get; set; }

    /// <summary>
    /// The synopsis, as plain text. Null for a series added before the shelf asked
    /// for one, and for a manual entry no source knows about. Empty when AniList was
    /// asked and has none — an answer, so the sync stops asking (SeriesRefresher).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The series' own cover, cached locally from the metadata source.
    /// </summary>
    // An addition to feature-spec §5, which puts covers only on Volume. AniList
    // publishes one cover per series, not per volume, so there is nowhere else to
    // put it — and a series with no volumes yet would otherwise have no cover at
    // all. Volume.CoverObjectKey stays for the real per-volume art MangaUpdates
    // provides, and wins over this when it exists.
    public string? CoverObjectKey { get; set; }

    /// <summary>The LN ↔ manga link. Null until a relation is synced.</summary>
    public Guid? RelatedSeriesId { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>
    /// How many volumes MangaUpdates counted at the last refresh. Null until one has.
    /// </summary>
    // Plain source metadata, the same class of fact as Volume.IsReleased and MangaUpdatesId,
    // not a derived state: it is what the release source said and nothing on the shelf can
    // recompute it. Kept because resetting a source choice has to put IsReleased back exactly
    // (a volume is out when its number is within this count), and re-reading it from an old
    // discrepancy row would mean parsing a log.
    public int? ReleaseSourceVolumeCount { get; set; }

    /// <summary>The width of <see cref="VolumeCountEvidence"/>, shared by its configuration and its writers.</summary>
    public const int VolumeCountEvidenceMaximumLength = 300;

    /// <summary>
    /// Where the published-volume count was read from. Null for a count decided before this
    /// was recorded.
    /// </summary>
    // Provenance for ReleaseSourceVolumeCount, written beside it and nowhere else: the same
    // class of fact as Volume.ReleaseDateSource. It cannot be recomputed later, because
    // MangaUpdates' notes change and the parser only ever saw the record of that night.
    public VolumeCountBasis? VolumeCountBasis { get; set; }

    /// <summary>
    /// The text the count was read from, verbatim and truncated to
    /// <see cref="VolumeCountEvidenceMaximumLength"/>: "Yen Press: 14 Volumes (Ongoing)", or
    /// the status line. Null when the basis has no text to quote.
    /// </summary>
    public string? VolumeCountEvidence { get; set; }

    public SeriesUserState? UserState { get; set; }
    public ICollection<Volume> Volumes { get; set; } = [];

    /// <summary>The user's picks of a source on recorded disagreements. User data.</summary>
    public ICollection<SourceChoice> SourceChoices { get; set; } = [];
}
