namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Clean record → wire contract, for the search endpoint.
/// </summary>
public static class SeriesSearchResultMapper
{
    extension(IReadOnlyList<SeriesSearchResult> results)
    {
        /// <param name="coverObjectKeys">
        /// Locally cached cover per AniList id, as far as caching succeeded.
        /// </param>
        public IReadOnlyList<SeriesSearchResultResponse> ToResponses(
            IReadOnlyDictionary<int, string> coverObjectKeys)
        {
            ArgumentNullException.ThrowIfNull(coverObjectKeys);

            return
            [
                .. results.Select(result => ToResponse(
                    result,
                    coverObjectKeys.TryGetValue(result.ExternalId, out var key) ? key : null)),
            ];
        }
    }

    private static SeriesSearchResultResponse ToResponse(SeriesSearchResult result, string? coverObjectKey) => new()
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
        CoverObjectKey = coverObjectKey,
    };
}
