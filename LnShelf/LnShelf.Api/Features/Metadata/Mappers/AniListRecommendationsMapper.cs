namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// What readers of a series also liked, out of the <c>Recommendations</c> query: the votes
/// worth trusting, in the series' own format, nothing adult, nothing repeated.
/// </summary>
public static class AniListRecommendationsMapper
{
    /// <summary>
    /// A pairing one reader made is an opinion, not a signal; two is the least that
    /// makes it a pattern.
    /// </summary>
    public const int MinimumVotes = 2;

    public static SeriesRecommendations ToRecommendations(AniListMedia series)
    {
        if (AniListMediaMapper.ToFormat(series.Format) is not { } seedFormat)
        {
            return SeriesRecommendations.None(series.Id);
        }

        var works = new List<RecommendedWork>();
        var seen = new HashSet<int>();

        foreach (var node in series.Recommendations?.Nodes ?? [])
        {
            if (node.MediaRecommendation is not { } work
                || work.IsAdult == true
                || node.Rating is not { } rating
                || rating < MinimumVotes)
            {
                continue;
            }

            // Same format as the seed: the light novel and its manga are separate series, and
            // a reader of one is not asking to be sent to the other.
            if (AniListMediaMapper.ToFormat(work.Format) != seedFormat
                || work.Id == series.Id
                || TitleOf(work) is not { } title
                || !seen.Add(work.Id))
            {
                continue;
            }

            works.Add(new RecommendedWork
            {
                Source = MetadataSourceName.AniList,
                ExternalId = work.Id,
                Title = title,
                Format = seedFormat,
                Votes = rating,
                CoverImageUrl = work.CoverImage?.Large.NullIfBlank(),
            });
        }

        return new SeriesRecommendations(
            series.Id,
            [.. works.OrderByDescending(work => work.Votes).ThenBy(work => work.ExternalId)]);
    }

    /// <summary>The English title, or romaji then native where AniList has none.</summary>
    private static string? TitleOf(AniListMedia work) =>
        work.Title?.English.NullIfBlank()
        ?? work.Title?.Romaji.NullIfBlank()
        ?? work.Title?.Native.NullIfBlank();
}
