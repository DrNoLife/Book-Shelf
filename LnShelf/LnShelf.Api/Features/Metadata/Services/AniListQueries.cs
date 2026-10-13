namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The GraphQL documents the shelf sends. Kept as literals rather than
/// generated from a schema: four queries do not justify a codegen step.
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
    //
    // staff is asked for here at six edges rather than the detail query's twelve.
    // It is what tells a light novel apart from its manga adaptation, which share
    // every title AniList publishes: the adaptation credits the novel's author as
    // Story, so only the artist separates them. Six edges is enough to reach both
    // credits before the translators start, and it rides along on a search the
    // import already pays for — the alternative is a detail request per candidate.
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
              staff(perPage: 6, sort: RELEVANCE) {
                edges {
                  role
                  node { name { full } }
                }
              }
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

    /// <summary>
    /// "More by {author}": one series' top staff, each with the works they are credited on.
    /// </summary>
    // One request rather than two. The staff id is not stored anywhere, so the alternative is
    // a lookup for the id and then a Staff(id) query — two of the thirty requests a minute
    // AniList allows, where nesting staffMedia under the staff edges spends one. Six edges
    // in relevance order is the search query's reasoning: author and illustrator come first,
    // and the tail is translators whose credits are fetched and then thrown away.
    //
    // type: MANGA is AniList's book side (novels, manga, one-shots); the mapper keeps only the
    // author's own credits and only the formats the shelf tracks.
    public const string AuthorWorks =
        """
        query AuthorWorks($id: Int!, $perPage: Int!) {
          Media(id: $id, type: MANGA) {
            id
            staff(perPage: 6, sort: RELEVANCE) {
              edges {
                role
                node {
                  name { full }
                  staffMedia(sort: [POPULARITY_DESC], type: MANGA, perPage: $perPage) {
                    edges {
                      staffRole
                      node {
                        id
                        format
                        title { english romaji native }
                        coverImage { large }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>
    /// What readers of a series also liked: one document for the series page's strip (one id)
    /// and For you (a batch).
    /// </summary>
    // Page.media(id_in) because a Page of 50 with 10 recommendations each was accepted by
    // AniList's complexity check (verified 2026-10-13). RATING_DESC puts the strongest votes
    // first. isAdult is asked so the mapper can drop adult works; type: MANGA is the book side.
    public const string Recommendations =
        """
        query Recommendations($ids: [Int], $perPage: Int!, $recommendationsPerSeries: Int!) {
          Page(page: 1, perPage: $perPage) {
            media(id_in: $ids, type: MANGA) {
              id
              format
              recommendations(perPage: $recommendationsPerSeries, sort: [RATING_DESC]) {
                nodes {
                  rating
                  mediaRecommendation {
                    id
                    format
                    isAdult
                    title { english romaji native }
                    coverImage { large }
                  }
                }
              }
            }
          }
        }
        """;
}
