namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The two GraphQL documents the shelf sends. Kept as literals rather than
/// generated from a schema: two queries do not justify a codegen step.
/// </summary>
internal static class AniListQueries
{
    /// <summary>
    /// Search. <c>type: MANGA</c> is AniList's book side (novels, manga and
    /// one-shots); anime is a different media type and never appears.
    /// </summary>
    // Sorted by popularity, not SEARCH_MATCH: "mushoku" under SEARCH_MATCH
    // returns six obscure entries before the two series a human means, because
    // every one of them matches the substring equally well. Popularity within
    // the search filter puts the light novel and the manga first, which is the
    // behaviour a search box needs.
    public const string SeriesSearch =
        """
        query SeriesSearch($search: String!, $formats: [MediaFormat], $perPage: Int!) {
          Page(page: 1, perPage: $perPage) {
            media(search: $search, type: MANGA, format_in: $formats, sort: [POPULARITY_DESC]) {
              id
              format
              status
              volumes
              startDate { year }
              title { english romaji native }
              coverImage { large }
            }
          }
        }
        """;

    /// <summary>
    /// One series in full. <c>staff</c> is capped at 12 edges — author and
    /// illustrator sit at the top of the relevance order, and the tail is
    /// translators and letterers the shelf does not store.
    /// </summary>
    // description is asked for here and deliberately not in the search query: a search
    // row prints a title, a format and a year, and a synopsis per row would be several
    // kilobytes of payload nothing renders.
    //
    // asHtml: false is the *less* marked-up of the two AniList offers, not clean text —
    // it still returns <br>, <i> and entities, which HtmlDescriptionSanitizer removes.
    public const string SeriesDetail =
        """
        query SeriesDetail($id: Int!) {
          Media(id: $id, type: MANGA) {
            id
            format
            status
            volumes
            startDate { year }
            title { english romaji native }
            coverImage { large }
            description(asHtml: false)
            genres
            staff(perPage: 12) {
              edges {
                role
                node { name { full } }
              }
            }
          }
        }
        """;
}
