namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>One search hit from a release source, before any detail is fetched.</summary>
public sealed record ReleaseSeriesCandidate
{
    /// <summary>
    /// The source's own id. A long, not an int: MangaUpdates ids are eleven
    /// digits (45348350266) and do not fit in an Int32.
    /// </summary>
    public required long ExternalId { get; init; }

    public required string Title { get; init; }

    /// <summary>The source's own medium word — "Novel", "Manga", "Doujinshi".</summary>
    // Kept deliberately, along with StartYear, although nothing in production reads either
    // yet. They are wire fidelity: the two fields a person disambiguating a bad link asks
    // about first ("is this the novel or the manga, and is it the right decade?"), and the
    // matcher is where that will be answered. Dropping them would mean re-deriving them from
    // a record the matcher no longer has.
    public string? SourceType { get; init; }

    public int? StartYear { get; init; }
}

/// <summary>
/// Everything a release source knows about one series.
/// </summary>
public sealed record ReleaseSeriesInfo
{
    public required long ExternalId { get; init; }
    public required string Title { get; init; }

    /// <summary>
    /// Alternative titles, which is what makes linking work at all: MangaUpdates
    /// files "86―EIGHTY-SIX" under the primary title "86" and the recognisable
    /// name only appears here.
    /// </summary>
    public IReadOnlyList<string> AssociatedTitles { get; init; } = [];

    public string? SourceType { get; init; }
    public int? StartYear { get; init; }

    /// <summary>
    /// Whether publication has finished, as the source sees it — read from its status text.
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// The same reading as <see cref="IsCompleted"/> with the whole word: Ongoing, Hiatus,
    /// Cancelled or Completed. What a source choice writes when the user takes this source's
    /// word on publication status.
    /// </summary>
    // Beside IsCompleted rather than replacing it: automatic behaviour still only ever writes
    // the "finished" direction, and keys on that boolean.
    public PublicationStatus PublicationStatus { get; init; }

    /// <summary>
    /// Volumes the source says exist, parsed out of its free-text status. Null
    /// when the text says nothing a parser should trust.
    /// </summary>
    public int? VolumeCount { get; init; }

    /// <summary>
    /// Which text <see cref="VolumeCount"/> was read from. Null when there is no count, or when
    /// the source cannot say.
    /// </summary>
    // Optional, so a release source that knows nothing of provenance still compiles and simply
    // records the basis as unknown.
    public VolumeCountBasis? CountBasis { get; init; }

    /// <summary>
    /// The text <see cref="VolumeCount"/> was read from, verbatim and already cut to
    /// <see cref="Series.VolumeCountEvidenceMaximumLength"/>.
    /// </summary>
    public string? CountEvidence { get; init; }

    /// <summary>
    /// Bonus volumes the English edition counts beside <see cref="VolumeCount"/> — the 1 in
    /// "9 Volumes + 1 Extra". Zero when the source states none.
    /// </summary>
    public int ExtraVolumeCount { get; init; }

    /// <summary>
    /// The status text the count came from, kept verbatim so a discrepancy row can
    /// show what the source actually said.
    /// </summary>
    public string? RawStatus { get; init; }
}
