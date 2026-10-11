using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Metadata;

/// <summary>
/// "More by {author}" on the series page: the other works AniList credits the series'
/// author with writing (<c>GET api/series/{id}/more-by-author</c>).
/// </summary>
public sealed record AuthorWorksResponse
{
    /// <summary>
    /// The author as AniList names them, or the series' own author when AniList credits none.
    /// Null when nobody is known, in which case <see cref="Works"/> is empty.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>Most popular first, the series itself left out.</summary>
    public IReadOnlyList<AuthorWorkResponse> Works { get; init; } = [];
}

/// <summary>One work by the author, in the shape the series page's small tiles need.</summary>
public sealed record AuthorWorkResponse
{
    /// <summary>What the Add button sends to <c>POST api/series</c>.</summary>
    public required int AniListId { get; init; }

    /// <summary>Never blank: romaji, then native, stand in for a missing English title.</summary>
    public required string TitleEnglish { get; init; }

    public required Format Format { get; init; }

    /// <summary>Locally cached cover, or null when caching it failed (design.md: no CDN URLs).</summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>
    /// The reader's own copy, when the work is already on their shelf: the tile then links to
    /// it ("On your shelf") instead of offering Add.
    /// </summary>
    public Guid? ShelfSeriesId { get; init; }
}
