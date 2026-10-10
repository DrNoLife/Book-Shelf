namespace LnShelf.Shared.Shelf;

/// <summary>
/// Every series on the current user's shelf by its external ids, so the search page can
/// tell a result that is already tracked from one that is not — and open it.
/// </summary>
// Its own response rather than a field on the search result: the search is cached across
// users for an hour, and "is it on my shelf" is per user and must not ride in that cache.
public sealed record TrackedSeriesIdsResponse
{
    public required IReadOnlyList<TrackedSeriesIds> Series { get; init; }
}

/// <summary>
/// One tracked series: the shelf's own id beside whichever catalogue ids it carries.
/// </summary>
// The shelf id rides along so the search preview's "On your shelf · Open" can link to the
// series page for a series added in an earlier session, not only for one added just now.
public sealed record TrackedSeriesIds
{
    public required Guid SeriesId { get; init; }

    public int? AniListId { get; init; }

    // A long for the same reason as on the search result: eleven-digit ids.
    public long? MangaUpdatesId { get; init; }
}
