namespace LnShelf.Shared.Metadata;

/// <summary>
/// A source the shelf takes metadata from. Also which one a disagreement was
/// resolved in favour of, where null means it was not resolved at all and is
/// waiting for a person.
/// </summary>
// Four, as of the volume-catalogue phase. It used to be exactly two, which was also baked
// into SyncDiscrepancy as a pair of named columns — a third source could not have been
// recorded at all. The discrepancy now carries a list of (source, value) pairs instead,
// which is what made the fourth a member here and nothing else.
[JsonConverter(typeof(JsonStringEnumConverter<MetadataSourceName>))]
public enum MetadataSourceName
{
    AniList,
    MangaUpdates,

    /// <summary>Per-volume dates, covers and titles, by ISBN.</summary>
    OpenLibrary,

    /// <summary>A store catalogue: a whole series' volume titles, blurbs, dates and art.</summary>
    AppleBooks,
}
