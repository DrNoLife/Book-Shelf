using LnShelf.Api.Features.Metadata.Models.MangaDex;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Which MangaDex record, of a title search's results, is this series — by the ids it links
/// to, never by title.
/// </summary>
// MangaDex has no lookup by AniList or MangaUpdates id, so a title search has to do — but its
// top hit is often a spin-off ("Slime" ranks Tensura Nikki first), so the search only proposes
// and the links decide. A record is accepted when its `al` link is this series' AniList id, or
// failing that its `mu` link is this series' MangaUpdates id. Nothing else is evidence.
public static class MangaDexSeriesMatcher
{
    /// <returns>The record linked to this series, or null when no result is.</returns>
    public static MangaDexManga? Pick(IEnumerable<MangaDexManga> records, int? aniListId, long? mangaUpdatesId)
    {
        ArgumentNullException.ThrowIfNull(records);

        var candidates = records.ToList();

        if (aniListId is { } aniList
            && candidates.FirstOrDefault(record => Link(record, "al") == aniList.ToString(CultureInfo.InvariantCulture))
                is { } byAniList)
        {
            return byAniList;
        }

        return mangaUpdatesId is { } mangaUpdates
            ? candidates.FirstOrDefault(record => IsMangaUpdatesLink(Link(record, "mu"), mangaUpdates))
            : null;
    }

    /// <summary>
    /// MangaUpdates' URL slug for a series id: the id in base 36, lower case ("ylx5wzn" is 75336092483).
    /// </summary>
    public static string ToMangaUpdatesSlug(long seriesId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seriesId);

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var slug = new StringBuilder();

        do
        {
            slug.Insert(0, digits[(int)(seriesId % 36)]);
            seriesId /= 36;
        }
        while (seriesId > 0);

        return slug.ToString();
    }

    // Case-insensitive, because the slug is a number written in letters, and without leading
    // zeros, because MangaUpdates pads its slugs to seven characters: Slime is "04gtb06", which
    // is 270105846. A legacy record can still carry the id in decimal.
    private static bool IsMangaUpdatesLink(string? link, long seriesId) =>
        link is not null
        && (string.Equals(link.TrimStart('0'), ToMangaUpdatesSlug(seriesId), StringComparison.OrdinalIgnoreCase)
            || link == seriesId.ToString(CultureInfo.InvariantCulture));

    private static string? Link(MangaDexManga record, string site) =>
        record.Attributes?.Links is { ValueKind: JsonValueKind.Object } links
        && links.TryGetProperty(site, out var value)
        && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
}
