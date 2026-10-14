using LnShelf.Shared.Metadata;
using LnShelf.Shared.Preferences;

namespace LnShelf.Shared.Shelf;

/// <summary>
/// Everything the series-detail page renders, in one round trip.
/// </summary>
public sealed record SeriesDetailResponse
{
    public required Guid SeriesId { get; init; }

    public required string TitleEnglish { get; init; }
    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }
    public required TrackingStatus TrackingStatus { get; init; }

    public string? Author { get; init; }
    public string? Illustrator { get; init; }

    /// <summary>Null for AniList-sourced series — it has no publisher field.</summary>
    public string? Publisher { get; init; }

    public int? StartYear { get; init; }
    public required IReadOnlyList<string> Genres { get; init; }

    /// <summary>
    /// The synopsis as plain text, paragraphs separated by a blank line. Null when no
    /// source has one — the hero then prints nothing rather than an empty panel.
    /// </summary>
    public string? Description { get; init; }

    public string? CoverObjectKey { get; init; }

    /// <summary>External ids, for the links out. Null hides its link.</summary>
    public int? AniListId { get; init; }

    public long? MangaUpdatesId { get; init; }

    public string? Notes { get; init; }
    public DateOnly? StartedAt { get; init; }
    public DateOnly? LastReadAt { get; init; }
    public required bool HasNewVolume { get; init; }

    /// <summary>
    /// The user silenced push notifications for this series. It still shows in "Since your
    /// last visit": muting is about pushes only.
    /// </summary>
    public bool NotificationsMuted { get; init; }

    /// <summary>
    /// This series' own volume view, or null to follow the global preference.
    /// </summary>
    // A per-series override rather than one global switch, because the right view is a
    // property of the series: 42 manga volumes want a list, 14 light novels with art
    // want tiles.
    public VolumeView? VolumeViewOverride { get; init; }

    /// <summary>Whether the reader counts this series in volumes or in chapters.</summary>
    // Defaulted rather than required, like the chapter fields below, so a payload from the
    // release before chapters existed still reads as the Volumes series it was.
    public ProgressUnit ProgressUnit { get; init; } = ProgressUnit.Volumes;

    /// <summary>
    /// The last chapter the reader has read. Kept when the unit is switched back to Volumes, so
    /// it is only shown while <see cref="ProgressUnit"/> is Chapters.
    /// </summary>
    public decimal? LastChapterRead { get; init; }

    /// <summary>The latest chapter MangaUpdates knows of. Null without a linked record.</summary>
    public decimal? LatestChapter { get; init; }

    /// <summary>"90 to read": chapters out that are not read. Null when either side is unknown.</summary>
    // Derived, never stored (CLAUDE.md), on the same footing as LibraryCardResponse.Behind.
    public decimal? ChaptersToRead => ChapterProgress.ToRead(LatestChapter, LastChapterRead);

    /// <summary>
    /// MangaDex's own id for this series, for the link on the chapter-range credit. Null until a
    /// chapter map was matched there.
    /// </summary>
    public Guid? MangaDexId { get; init; }

    /// <summary>
    /// The title of the English Wikipedia page whose chapter list mapped this series, for the
    /// link on the chapter-range credit. Null until one was verified.
    /// </summary>
    public string? ChapterListPage { get; init; }

    /// <summary>
    /// Volumes above the highest one read that the last chapter read has carried the reader
    /// past: what "You've finished Vol 12 — mark it read?" offers. Empty unless the series is
    /// counted in chapters.
    /// </summary>
    // Derived, never stored (CLAUDE.md), on the same footing as ChaptersToRead. Read straight
    // off the response by the series page and by the library after "+1 chapter", so both offer
    // exactly the same volumes.
    //
    // Not serialized, unlike ChaptersToRead: that is one number, this is a second copy of
    // volumes already in Volumes, and the client computes it from the same fields anyway.
    [JsonIgnore]
    public IReadOnlyList<VolumeResponse> VolumesFinishedByChapter => ProgressUnit is ProgressUnit.Chapters
        ? ChapterProgress.VolumesFinishedBy(LastChapterRead, Volumes)
        : [];

    /// <summary>The sources any volume's chapter range came from, for the credit line. Empty without ranges.</summary>
    // Not serialized, for the same reason as VolumesFinishedByChapter: it repeats what the
    // volumes already carry.
    [JsonIgnore]
    public IReadOnlyList<MetadataSourceName> ChapterRangeSources =>
    [
        .. Volumes
            .Where(volume => volume.ChapterRangeSource is not null)
            .Select(volume => volume.ChapterRangeSource!.Value)
            .Distinct()
            .Order(),
    ];

    public required SeriesCountsResponse Counts { get; init; }

    /// <summary>Where the published count came from, for "How is this counted?".</summary>
    public CountProvenanceResponse CountProvenance { get; init; } = new();

    /// <summary>
    /// Null when no volume has a purchase price.
    /// </summary>
    // Null rather than zeroes: "0 kr · avg 0 kr" is a lie about a shelf whose prices
    // were simply never recorded, and an imported library has none.
    public SeriesSpendResponse? Spend { get; init; }

    /// <summary>Null until a relation is synced (phase 2's AniList relations).</summary>
    public SeriesRelationResponse? Relation { get; init; }

    public required IReadOnlyList<VolumeResponse> Volumes { get; init; }

    /// <summary>
    /// The sources the user has picked on this series' disagreements — what the quiet
    /// "Using X (your choice)" markers read.
    /// </summary>
    public IReadOnlyList<SourceChoiceResponse> SourceChoices { get; init; } = [];
}

/// <summary>The hero's numbers, on the same rules as the library card.</summary>
public sealed record SeriesCountsResponse
{
    public required int Read { get; init; }
    public required int Owned { get; init; }
    public required int Published { get; init; }
    public required int ToBuy { get; init; }
    public required int Backlog { get; init; }
    public required int Tracked { get; init; }

    /// <summary>Feeds the "Vol 12 on Saxo" call to action. Null when caught up.</summary>
    public decimal? NextToBuyNumber { get; init; }

    /// <summary>Volumes the shelf knows of that are not out yet.</summary>
    public required int NotOut { get; init; }

    /// <summary>Of those, how many carry a date.</summary>
    public required int Dated { get; init; }
}

/// <summary>
/// The spend line — feature-spec §4.7's one v1 exception to Stats being post-v1.
/// </summary>
public sealed record SeriesSpendResponse
{
    /// <summary>Volumes that actually carry a price, not volumes owned.</summary>
    public required int PricedCount { get; init; }

    public required decimal Total { get; init; }
    public required decimal Average { get; init; }
}

/// <summary>The LN ↔ manga link, when one has been synced.</summary>
public sealed record SeriesRelationResponse
{
    public required Guid SeriesId { get; init; }
    public required string TitleEnglish { get; init; }
    public required Format Format { get; init; }
    public required int Published { get; init; }
    public required int Owned { get; init; }
}
