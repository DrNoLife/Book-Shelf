namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The works a series' author is credited with writing, as the metadata source lists them,
/// most popular first. Holds no reader's data: one copy serves every shelf.
/// </summary>
/// <param name="AuthorName">Null when the source credits no author on the series.</param>
public sealed record AuthorWorks(string? AuthorName, IReadOnlyList<AuthorWork> Works)
{
    public static AuthorWorks None { get; } = new(AuthorName: null, Works: []);
}

/// <summary>One work by the author.</summary>
public sealed record AuthorWork
{
    public required int AniListId { get; init; }

    /// <summary>Never blank: romaji, then native, stand in for a missing English title.</summary>
    public required string Title { get; init; }

    public required Format Format { get; init; }

    /// <summary>The source's cover URL; never handed to the browser.</summary>
    public string? CoverImageUrl { get; init; }

    /// <summary>The local cache's key for the cover, once it has been fetched.</summary>
    public string? CoverObjectKey { get; init; }
}
