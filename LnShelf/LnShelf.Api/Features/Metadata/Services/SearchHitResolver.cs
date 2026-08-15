namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Full detail for one search hit, from the source that produced it.
/// </summary>
// The other half of ISeriesSearchService's job, kept apart from it: searching decides
// which source answers, and this follows that decision to the right detail endpoint. Once
// a search can come back from either source, "look the hit up" is no longer a single call
// — and the dispatch belongs in one place rather than at each caller.
public sealed class SearchHitResolver(
    ISeriesMetadataSource aniListSource,
    IMangaUpdatesSeriesQueryService mangaUpdatesSource)
{
    /// <returns>Null when the source has no detail for that id.</returns>
    public async Task<ResolvedSeries?> ResolveAsync(
        SeriesSearchResult hit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hit);

        switch (hit.Source)
        {
            case MetadataSourceName.AniList:
                var media = await aniListSource.GetSeriesAsync(hit.AniListId, cancellationToken);
                return media is null ? null : ResolvedSeriesMapper.ToResolvedSeries(media);

            case MetadataSourceName.MangaUpdates:
                var record = await mangaUpdatesSource.GetSeriesAsync(hit.SourceId, cancellationToken);
                return record is null ? null : ResolvedSeriesMapper.ToResolvedSeries(record);

            default:
                // Open Library and Apple Books are per-volume sources; neither can be asked
                // about a series, so a hit claiming to come from one is a defect here.
                throw new ArgumentOutOfRangeException(
                    nameof(hit), hit.Source, "No series-level source answers for this hit.");
        }
    }
}
