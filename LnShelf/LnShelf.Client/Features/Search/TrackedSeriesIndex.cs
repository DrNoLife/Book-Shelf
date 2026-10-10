namespace LnShelf.Client.Features.Search;

/// <summary>
/// The user's shelf, looked up by the catalogue ids a search result carries: which results
/// are already on it, and which series page each one opens.
/// </summary>
// Kept apart from the results because the search response is cached across users; this is
// fetched per search so "already on the shelf" is true for a series added before this
// session as well as one added in it.
public sealed class TrackedSeriesIndex
{
    private readonly Dictionary<int, Guid> _byAniListId = [];
    private readonly Dictionary<long, Guid> _byMangaUpdatesId = [];

    /// <summary>Replaces everything known with the shelf as the API reported it.</summary>
    public void Load(TrackedSeriesIdsResponse? tracked)
    {
        _byAniListId.Clear();
        _byMangaUpdatesId.Clear();

        foreach (var series in tracked?.Series ?? [])
        {
            Remember(series.AniListId, series.MangaUpdatesId, series.SeriesId);
        }
    }

    /// <summary>Records a series just added, so its row flips without another round trip.</summary>
    public void Remember(SeriesSearchResultResponse result, Guid seriesId)
    {
        ArgumentNullException.ThrowIfNull(result);

        Remember(result.AniListId, result.MangaUpdatesId, seriesId);
    }

    /// <returns>Null when the result is not on the shelf.</returns>
    public Guid? SeriesIdOf(SeriesSearchResultResponse result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.AniListId is { } aniListId && _byAniListId.TryGetValue(aniListId, out var byAniList))
        {
            return byAniList;
        }

        return result.MangaUpdatesId is { } mangaUpdatesId && _byMangaUpdatesId.TryGetValue(mangaUpdatesId, out var byMangaUpdates)
            ? byMangaUpdates
            : null;
    }

    private void Remember(int? aniListId, long? mangaUpdatesId, Guid seriesId)
    {
        if (aniListId is { } knownAniListId) _byAniListId[knownAniListId] = seriesId;
        if (mangaUpdatesId is { } knownMangaUpdatesId) _byMangaUpdatesId[knownMangaUpdatesId] = seriesId;
    }
}
