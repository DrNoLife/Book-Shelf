namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// "More by {author}": the other works a series' author wrote, each marked when it is
/// already on the reader's shelf.
/// </summary>
// Its own seam rather than a method on ISeriesMetadataSource: only AniList can answer it (the
// question needs staff credits per work), and every other metadata source would have to
// implement a member it has nothing to say about.
public interface IAuthorWorksQueryService
{
    /// <returns>
    /// Null when the shelf has no such series. Empty works when the series has no AniList id,
    /// or AniList credits it with no author.
    /// </returns>
    Task<AuthorWorksResponse?> GetForSeriesAsync(Guid seriesId, CancellationToken cancellationToken);
}
