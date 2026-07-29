namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class SeriesDetailQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : ISeriesDetailQueryService
{
    public async Task<SeriesDetailResponse?> GetAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await LoadAsync(dbContext, seriesId, cancellationToken);
        if (series is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
        var detail = SeriesDetailProjection.ToDetail(series, today);

        return detail with { Relation = await LoadRelationAsync(series, today, cancellationToken) };
    }

    /// <summary>The whole page's data in one query — no per-volume round trips.</summary>
    internal static Task<Series?> LoadAsync(
        LnShelfDbContext dbContext,
        Guid seriesId,
        CancellationToken cancellationToken) =>
        dbContext.Series
            .AsNoTracking()
            .Include(series => series.UserState)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(series => series.Id == seriesId, cancellationToken);

    /// <summary>
    /// The LN ↔ manga link. Nothing populates <c>RelatedSeriesId</c> yet — AniList
    /// relations are still unbuilt — so this is the seam rendering nothing rather
    /// than a feature waiting to be written.
    /// </summary>
    private async Task<SeriesRelationResponse?> LoadRelationAsync(
        Series series,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (series.RelatedSeriesId is not { } relatedSeriesId)
        {
            return null;
        }

        var related = await dbContext.Series
            .AsNoTracking()
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == relatedSeriesId, cancellationToken);

        if (related is null)
        {
            return null;
        }

        var volumes = related.Volumes.Select(volume => SeriesDetailProjection.ToVolume(volume, today)).ToList();

        return new SeriesRelationResponse
        {
            SeriesId = related.Id,
            TitleEnglish = related.TitleEnglish,
            Format = related.Format,
            Published = volumes.Count(volume => volume.IsOut),
            Owned = volumes.Count(volume => volume.Owned),
        };
    }
}
