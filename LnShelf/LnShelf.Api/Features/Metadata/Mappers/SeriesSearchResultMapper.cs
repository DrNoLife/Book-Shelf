namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Clean record → wire contract, for the search endpoint.
/// </summary>
public static class SeriesSearchResultMapper
{
    extension(IReadOnlyList<SeriesSearchResult> results)
    {
        public IReadOnlyList<SeriesSearchResultResponse> ToResponses() => [.. results.Select(ToResponse)];
    }

    private static SeriesSearchResultResponse ToResponse(SeriesSearchResult result) => new()
    {
        // The contract names the source explicitly (feature-spec §6's
        // POST /api/series { aniListId, format }), so the client can hand the id
        // straight back without knowing anything else about AniList.
        AniListId = result.ExternalId,
        TitleEnglish = result.TitleEnglish,
        TitleRomaji = result.TitleRomaji,
        TitleNative = result.TitleNative,
        Format = result.Format,
        PublicationStatus = result.PublicationStatus,
        VolumeCount = result.VolumeCount,
        StartYear = result.StartYear,
    };
}
