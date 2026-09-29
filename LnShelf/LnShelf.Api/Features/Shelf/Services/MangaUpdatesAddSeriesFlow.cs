namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
// Exists because AniList disabling its public API left the app unable to add anything at
// all. It is not a preference: the AniList flow stays the first choice, and this is what
// the search page's Add button reaches when the results came from the fallback.
public sealed class MangaUpdatesAddSeriesFlow(
    IMangaUpdatesSeriesQueryService mangaUpdatesSource,
    ISeriesCoverCache seriesCoverCache,
    TimeProvider timeProvider,
    ILogger<MangaUpdatesAddSeriesFlow> logger) : IAddSeriesFlow
{
    public MetadataSourceName Source => MetadataSourceName.MangaUpdates;

    public long SourceIdOf(AddSeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mangaUpdatesId = request.MangaUpdatesId
            ?? throw new ArgumentException("The request carries no MangaUpdates id.", nameof(request));

        // A 400 for the same reason as the AniList flow's: the id is the caller's input.
        if (mangaUpdatesId <= 0)
        {
            throw new InvalidInputException($"{mangaUpdatesId} is not a MangaUpdates id.");
        }

        return mangaUpdatesId;
    }

    /// <inheritdoc />
    // Idempotency matters more here than on the AniList side, because a series linked to
    // MangaUpdates by a nightly sync — never added from it — is still a hit, and adding it
    // again would otherwise be a unique-index violation rather than a polite no-op.
    public Expression<Func<Series, bool>> Identifies(long sourceId) =>
        series => series.MangaUpdatesId == sourceId;

    public async Task<Series?> BuildAsync(
        AddSeriesRequest request,
        long sourceId,
        CancellationToken cancellationToken)
    {
        var metadata = await mangaUpdatesSource.GetSeriesAsync(sourceId, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var coverObjectKey = await seriesCoverCache.EnsureCachedAsync(
            MetadataSourceName.MangaUpdates, metadata.SourceId, metadata.CoverImageUrl, cancellationToken);

        var series = SeriesMetadataMapper.ToTrackedSeries(
            metadata, timeProvider.GetUtcNow(), coverObjectKey);

        // Logged here rather than folded into the pipeline's "Added" line, because the raw
        // status is the only thing this source knows that the stored row does not: the
        // volume count is parsed out of prose, and the prose is what makes a wrong count
        // diagnosable afterwards.
        logger.LogInformation(
            "MangaUpdates {SourceId} reports {VolumeCount} volumes from status {RawStatus}",
            metadata.SourceId,
            series.Volumes.Count,
            metadata.RawStatus);

        return series;
    }
}
