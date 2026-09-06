namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class LibraryQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : ILibraryQueryService
{
    public async Task<LibraryResponse> GetLibraryAsync(
        Format? format,
        LibraryFilter filter,
        CancellationToken cancellationToken)
    {
        var today = timeProvider.Today();

        var query = dbContext.Series.AsNoTracking();
        if (format is not null)
        {
            query = query.Where(series => series.Format == format);
        }

        // One query for the whole format scope, projected straight to cards. The
        // header totals and sidebar shelf counts are counted across that scope,
        // so fetching it once and aggregating in memory costs one round trip
        // where per-shelf SQL counts would cost seven. This is a single user's
        // few hundred series; if the shelf ever grows enough to matter, the
        // aggregates move into the query and this comment is the reason to look.
        var scoped = await query
            .Select(LibraryCardProjection.ToCard(today))
            .ToListAsync(cancellationToken);

        return new LibraryResponse
        {
            Totals = CreateTotals(scoped),
            Shelves = CreateShelfCounts(scoped),
            Cards = scoped
                .Where(card => card.Matches(filter))
                // Most behind first — what the grid footer claims it is doing.
                .OrderByDescending(card => card.ToBuy + card.Backlog)
                .ThenBy(card => card.TitleEnglish, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static LibraryTotals CreateTotals(List<LibraryCardResponse> cards) => new()
    {
        SeriesCount = cards.Count,
        Read = cards.Sum(card => card.Read),
        Owned = cards.Sum(card => card.Owned),
        Published = cards.Sum(card => card.Published),
        Backlog = cards.Sum(card => card.Backlog),
        ToBuy = cards.Sum(card => card.ToBuy),
    };

    private static LibraryShelfCounts CreateShelfCounts(List<LibraryCardResponse> cards) => new()
    {
        Reading = cards.Count(card => card.Matches(LibraryFilter.Reading)),
        BehindOnBuying = cards.Count(card => card.Matches(LibraryFilter.Behind)),
        BacklogVolumes = cards.Sum(card => card.Backlog),
        UpToDate = cards.Count(card => card.Matches(LibraryFilter.UpToDate)),
        Completed = cards.Count(card => card.Matches(LibraryFilter.Completed)),
        PlanToStart = cards.Count(card => card.Matches(LibraryFilter.PlanToStart)),
        Dropped = cards.Count(card => card.Matches(LibraryFilter.Dropped)),
    };
}
