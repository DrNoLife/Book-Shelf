namespace LnShelf.Shared.Metadata;

/// <summary>
/// A source the shelf takes metadata from. Also which one a disagreement was
/// resolved in favour of, where null means it was not resolved at all and is
/// waiting for a person.
/// </summary>
// Three, as of the per-volume enrichment phase. It used to be exactly two, which was
// also baked into SyncDiscrepancy as a pair of named columns — a third source could
// not have been recorded at all. The discrepancy now carries a list of (source, value)
// pairs instead, so a fourth is a member here and nothing else.
[JsonConverter(typeof(JsonStringEnumConverter<MetadataSourceName>))]
public enum MetadataSourceName
{
    AniList,
    MangaUpdates,

    /// <summary>Per-volume dates, covers and titles, by ISBN.</summary>
    OpenLibrary,
}
