namespace LnShelf.Shared.Shelf;

/// <summary>
/// Adds a tracked series from AniList (feature-spec §6). A series with no
/// external id — a WebNovel — arrives through <c>POST /api/series/manual</c>
/// instead, which does not exist yet.
/// </summary>
public sealed record AddSeriesRequest
{
    public required int AniListId { get; init; }

    /// <summary>
    /// Which of the two series the caller means. The AniList id already
    /// determines this, so it is the caller's declaration rather than the
    /// authority: where they disagree the source wins and the response says
    /// which format was actually stored.
    /// </summary>
    public required Format Format { get; init; }
}
