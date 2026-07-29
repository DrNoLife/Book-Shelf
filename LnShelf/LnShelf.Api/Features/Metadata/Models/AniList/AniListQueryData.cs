namespace LnShelf.Api.Features.Metadata.Models.AniList;

/// <summary>
/// The <c>data</c> shape of <c>AniListQueries.SeriesSearch</c>.
/// </summary>
// AniList capitalises its root fields ("Page", "Media"); the Web serializer
// defaults match case-insensitively, so no [JsonPropertyName] is needed.
public sealed class AniListSearchData
{
    public AniListPage? Page { get; set; }
}

/// <inheritdoc cref="AniListSearchData"/>
public sealed class AniListPage
{
    public List<AniListMedia>? Media { get; set; }
}

/// <summary>
/// The <c>data</c> shape of <c>AniListQueries.SeriesDetail</c>.
/// </summary>
public sealed class AniListMediaData
{
    public AniListMedia? Media { get; set; }
}
