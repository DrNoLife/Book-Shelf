namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
// No owner filter here on purpose: the DbContext's query filter already scopes Series to
// the current user.
public sealed class TrackedSeriesQueryService(LnShelfDbContext dbContext) : ITrackedSeriesQueryService
{
    public async Task<TrackedSeriesIdsResponse> GetTrackedIdsAsync(CancellationToken cancellationToken)
    {
        // A series with neither id cannot match a search result, so it is left out.
        var series = await dbContext.Series.AsNoTracking()
            .Where(candidate => candidate.AniListId != null || candidate.MangaUpdatesId != null)
            .Select(candidate => new TrackedSeriesIds
            {
                SeriesId = candidate.Id,
                AniListId = candidate.AniListId,
                MangaUpdatesId = candidate.MangaUpdatesId,
            })
            .ToListAsync(cancellationToken);

        return new TrackedSeriesIdsResponse { Series = series };
    }
}
