namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One search, and which source ended up answering it.
/// </summary>
// The source belongs on the answer rather than being assumed by the caller, because it
// is not fixed: when AniList refuses to serve, the same query is answered by
// MangaUpdates instead, and everything downstream — the cover key, the id the client
// hands back to Add, the note the page prints — depends on knowing which happened.
public sealed record SeriesSearchOutcome
{
    public required MetadataSourceName Source { get; init; }

    public required IReadOnlyList<SeriesSearchResult> Results { get; init; }

    /// <summary>
    /// One line explaining the substitution, set only when the preferred source could
    /// not answer. Null on a normal search — there is nothing to say.
    /// </summary>
    public string? DegradedNote { get; init; }
}
