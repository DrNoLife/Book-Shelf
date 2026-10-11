namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class AniListAddSeriesFlow(
    ISeriesMetadataSource metadataSource,
    ICoverCache coverCache,
    AddTimeReleaseLink releaseLink,
    TimeProvider timeProvider) : IAddSeriesFlow
{
    public MetadataSourceName Source => MetadataSourceName.AniList;

    public long SourceIdOf(AddSeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var aniListId = request.AniListId
            ?? throw new ArgumentException("The request carries no AniList id.", nameof(request));

        // A 400, not ArgumentOutOfRangeException: the id is straight from the request
        // body, and anything but InvalidInputException reaches the caller as a 500.
        if (aniListId <= 0)
        {
            throw new InvalidInputException($"{aniListId} is not an AniList id.");
        }

        return aniListId;
    }

    /// <inheritdoc />
    // Narrowed to an int outside the expression rather than cast inside it: the captured
    // constant is then the column's own type and EF compares it directly, where a cast in
    // the tree would ask the database to convert every row's id before comparing.
    public Expression<Func<Series, bool>> Identifies(long sourceId)
    {
        var aniListId = (int)sourceId;

        return series => series.AniListId == aniListId;
    }

    public async Task<Series?> BuildAsync(
        AddSeriesRequest request,
        long sourceId,
        CancellationToken cancellationToken)
    {
        var metadata = await metadataSource.GetSeriesAsync((int)sourceId, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        // Fetched now rather than on first render: the shelf has to work with the
        // AniList CDN down, which means the bytes must already be local.
        var coverObjectKey = await coverCache.EnsureCachedAsync(
            metadata.ExternalId, metadata.CoverImageUrl, cancellationToken);

        var series = SeriesMetadataMapper.ToTrackedSeries(
            metadata, timeProvider.GetUtcNow(), coverObjectKey);

        // Before the reader's starting state is applied, so "read 9" lands on the volumes the
        // release source counts rather than on AniList's.
        await releaseLink.ApplyAsync(series, cancellationToken);

        return series;
    }
}
