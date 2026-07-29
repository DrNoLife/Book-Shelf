namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class LibraryQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : ILibraryQueryService
{
    public async Task<IReadOnlyList<LibraryCardResponse>> GetLibraryAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);

        // One query, projected straight to the card model — no per-series
        // round trips, no tracking, and nothing derived is read from a column.
        return await dbContext.Series
            .AsNoTracking()
            .OrderBy(series => series.TitleEnglish)
            .Select(LibraryCardProjection.ToCard(today))
            .ToListAsync(cancellationToken);
    }
}
