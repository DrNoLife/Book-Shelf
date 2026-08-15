namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Clean record → wire contract, for the search endpoint.
/// </summary>
public static class SeriesSearchResultMapper
{
    extension(IReadOnlyList<SeriesSearchResult> results)
    {
        /// <param name="coverObjectKeys">
        /// Locally cached cover per source id, as far as caching succeeded.
        /// </param>
        public IReadOnlyList<SeriesSearchResultResponse> ToResponses(
            IReadOnlyDictionary<long, string> coverObjectKeys)
        {
            ArgumentNullException.ThrowIfNull(coverObjectKeys);

            return
            [
                .. results.Select(result => ToResponse(
                    result,
                    coverObjectKeys.TryGetValue(result.SourceId, out var key) ? key : null)),
            ];
        }
    }

    private static SeriesSearchResultResponse ToResponse(SeriesSearchResult result, string? coverObjectKey) => new()
    {
        // The contract names the source and carries that source's id, so the client can
        // hand the pair straight back to POST /api/series without knowing anything else
        // about either API — and so a MangaUpdates row cannot be added as an AniList one.
        Source = result.Source,
        AniListId = result.Source is MetadataSourceName.AniList ? result.AniListId : null,
        MangaUpdatesId = result.Source is MetadataSourceName.MangaUpdates ? result.SourceId : null,
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
