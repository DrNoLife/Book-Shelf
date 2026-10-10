namespace LnShelf.Shared.Shelf;

/// <summary>
/// The external ids of every series on the current user's shelf, so the search page can
/// tell a result that is already tracked from one that is not.
/// </summary>
// Its own response rather than a field on the search result: the search is cached across
// users for an hour, and "is it on my shelf" is per user and must not ride in that cache.
public sealed record TrackedSeriesIdsResponse
{
    public required IReadOnlyList<int> AniListIds { get; init; }

    // A long for the same reason as on the search result: eleven-digit ids.
    public required IReadOnlyList<long> MangaUpdatesIds { get; init; }
}
