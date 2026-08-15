namespace LnShelf.Shared.Metadata;

/// <summary>
/// One search: the hits, and which source answered.
/// </summary>
// A wrapper rather than the bare list this used to be, matching LibraryResponse's shape
// for the same reason: the page needs one fact about the whole answer — that the usual
// source could not be reached — and a per-row flag would print that fact once per result.
public sealed record SeriesSearchResponse
{
    /// <summary>The source every hit in <see cref="Results"/> came from.</summary>
    public required MetadataSourceName Source { get; init; }

    public required IReadOnlyList<SeriesSearchResultResponse> Results { get; init; }

    /// <summary>
    /// One quiet line for the page to print, set only when the preferred source could
    /// not answer and another one did. Null on a normal search.
    /// </summary>
    public string? DegradedNote { get; init; }
}
