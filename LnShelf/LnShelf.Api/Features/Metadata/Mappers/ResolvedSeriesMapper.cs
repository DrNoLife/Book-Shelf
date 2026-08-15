namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Either source's full record → the source-neutral shape <c>enrichment-verify</c> reads.
/// </summary>
// Clean → clean, and still a mapper: the point is that the two sources describe a series
// differently and exactly one place should know how.
public static class ResolvedSeriesMapper
{
    public static ResolvedSeries ToResolvedSeries(SeriesMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ResolvedSeries
        {
            Source = MetadataSourceName.AniList,
            SourceId = metadata.ExternalId,
            TitleEnglish = metadata.TitleEnglish,
            TitleRomaji = metadata.TitleRomaji,
            TitleNative = metadata.TitleNative,
            Format = metadata.Format,
            PublicationStatus = metadata.PublicationStatus,
            Description = metadata.Description,
            CoverImageUrl = metadata.CoverImageUrl,
            ListedVolumeCount = metadata.Volumes.Count,

            // AniList is not the release source, so it never sets this. The matcher is
            // what establishes a count on this path, as it always has.
            ReleaseVolumeCount = null,
        };
    }

    public static ResolvedSeries ToResolvedSeries(MangaUpdatesSeriesMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ResolvedSeries
        {
            Source = MetadataSourceName.MangaUpdates,
            SourceId = metadata.SourceId,
            TitleEnglish = metadata.TitleEnglish,

            // MangaUpdates labels none of its alternative titles, so there is nothing
            // honest to put in either of these (see MangaUpdatesSeriesMetadata).
            TitleRomaji = null,
            TitleNative = null,

            Format = metadata.Format,
            PublicationStatus = metadata.PublicationStatus,
            Description = metadata.Description,
            CoverImageUrl = metadata.CoverImageUrl,

            // No volume entities: MangaUpdates publishes a count in prose and nothing per
            // volume, which is the whole reason IReleaseSource promises what it does.
            ListedVolumeCount = 0,

            ReleaseVolumeCount = metadata.VolumeCount,
        };
    }
}
