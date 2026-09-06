namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// A per-volume source's record → the <see cref="VolumeFacts"/> the writer arbitrates over.
/// </summary>
// Both per-volume seams answer in their own shape, and neither shape is what the conflict
// rules read. Mapping them here rather than inside the enrichment passes keeps the two
// translations side by side, where the fields one source carries and the other does not are
// visible at a glance (CLAUDE.md — Mapping).
public static class VolumeFactsMapper
{
    /// <param name="sourceName">
    /// The seam's own name, so the writer records who a fact came from rather than which
    /// class asked.
    /// </param>
    public static VolumeFacts ToFacts(CatalogueVolume record, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new VolumeFacts
        {
            Source = MetadataSourceName.AppleBooks,
            SourceName = sourceName,
            SourceRecordKey = record.SourceRecordKey,
            Title = record.Title,
            Description = record.Description,
            ReleaseDate = record.ReleaseDate,

            // Nothing to keep: a storefront date is either a full ISO timestamp or absent, so
            // there is no coarse-but-real string of the "2022" kind to report.
            RawReleaseDate = null,
            CoverImageUrl = record.ArtworkUrl,
        };
    }

    public static VolumeFacts ToFacts(VolumeEnrichment enrichment, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(enrichment);

        return new VolumeFacts
        {
            Source = MetadataSourceName.OpenLibrary,
            SourceName = sourceName,
            SourceRecordKey = enrichment.SourceRecordKey,
            Title = enrichment.Title,

            // Open Library carries no blurbs for this shelf's editions at all — measured 0 of
            // 15 — so there is nothing to map and WorthAskingByIsbn does not ask on their
            // account.
            Description = null,
            ReleaseDate = enrichment.ReleaseDate,
            RawReleaseDate = enrichment.RawPublishDate,
            CoverImageUrl = enrichment.CoverImageUrl,
        };
    }
}
