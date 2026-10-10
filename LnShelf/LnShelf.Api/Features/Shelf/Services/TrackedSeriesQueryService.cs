namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
// No owner filter here on purpose: the DbContext's query filter already scopes Series to
// the current user.
public sealed class TrackedSeriesQueryService(LnShelfDbContext dbContext) : ITrackedSeriesQueryService
{
    public async Task<TrackedSeriesIdsResponse> GetTrackedIdsAsync(CancellationToken cancellationToken)
    {
        var aniListIds = await dbContext.Series.AsNoTracking()
            .Where(series => series.AniListId != null)
            .Select(series => series.AniListId!.Value)
            .ToListAsync(cancellationToken);

        var mangaUpdatesIds = await dbContext.Series.AsNoTracking()
            .Where(series => series.MangaUpdatesId != null)
            .Select(series => series.MangaUpdatesId!.Value)
            .ToListAsync(cancellationToken);

        return new TrackedSeriesIdsResponse
        {
            AniListIds = aniListIds,
            MangaUpdatesIds = mangaUpdatesIds,
        };
    }
}
