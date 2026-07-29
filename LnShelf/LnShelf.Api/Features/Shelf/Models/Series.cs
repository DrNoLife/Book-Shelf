namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// The trackable entity (feature-spec §5). The light novel and the manga of the
/// same title are two separate series, linked through <see cref="RelatedSeriesId"/>.
/// </summary>
// Persistence models are mutable classes rather than the records CLAUDE.md
// prefers: EF materialisation and change tracking need settable members. They
// never leave this project — queries project to LnShelf.Shared records.
public sealed class Series
{
    public Guid Id { get; set; }

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

    public PublicationStatus PublicationStatus { get; set; }
    public string? Publisher { get; set; }
    public int? StartYear { get; set; }
    public string[] Genres { get; set; } = [];
    public string? Author { get; set; }
    public string? Illustrator { get; set; }

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

    public SeriesUserState? UserState { get; set; }
    public ICollection<Volume> Volumes { get; set; } = [];
}
