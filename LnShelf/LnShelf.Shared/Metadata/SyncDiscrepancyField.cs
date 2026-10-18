namespace LnShelf.Shared.Metadata;

/// <summary>
/// What two metadata sources disagreed about.
/// </summary>
// feature-spec §5 lists VolumeCount, ReleaseDate and Status. SeriesLink is added
// here: "these two sources do not agree on which series this even is" is a
// disagreement of exactly the same kind, it needs the same human resolution, and
// giving it the same home means the review list is one query rather than two.
[JsonConverter(typeof(StrictStringEnumConverter<SyncDiscrepancyField>))]
public enum SyncDiscrepancyField
{
    VolumeCount,
    ReleaseDate,
    Status,

    /// <summary>No confident link between the shelf's series and the release source's.</summary>
    SeriesLink,

    /// <summary>Two chapter-map sources start one volume on chapters more than one apart.</summary>
    // Settled by the sources' precedence every time, so it is shown but never chosen: there is
    // no override column for a chapter range, and a person has nothing better to go on.
    ChapterRange,
}
