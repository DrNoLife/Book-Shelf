namespace LnShelf.Shared.Shelf;

/// <summary>
/// Adds a tracked series from a metadata source (feature-spec §6). Exactly one of
/// the two ids is set — whichever source the search result came from. A series with
/// no external id — a WebNovel — arrives through <c>POST /api/series/manual</c>
/// instead, which does not exist yet.
/// </summary>
// Both ids are optional and neither is <c>required</c>, because "exactly one" is not
// something the type system can state: the rule lives in AddSeriesSource, which is
// also what a 400 comes from.
public sealed record AddSeriesRequest
{
    public int? AniListId { get; init; }

    /// <summary>Eleven digits, so a long (see <c>SeriesSearchResultResponse</c>).</summary>
    public long? MangaUpdatesId { get; init; }

    /// <summary>
    /// Which of the two series the caller means. The external id already
    /// determines this, so it is the caller's declaration rather than the
    /// authority: where they disagree the source wins and the response says
    /// which format was actually stored.
    /// </summary>
    public required Format Format { get; init; }

    /// <summary>
    /// "Start with": volumes 1..N are marked read as the series goes on the shelf. Null or
    /// zero adds it untouched, on Plan to start; anything above zero puts it on Reading.
    /// </summary>
    // No read date is stamped on them: the add does not know when they were read, and
    // today would be a date the stats page then believes.
    public int? InitialReadCount { get; init; }

    /// <summary>
    /// "Start with": volumes 1..N are marked owned. Never fewer than
    /// <see cref="InitialReadCount"/> — the add raises it to that, because the start-with
    /// control treats a read volume as one on the shelf.
    /// </summary>
    public int? InitialOwnedCount { get; init; }

    /// <summary>
    /// The highest start-with count accepted for a series whose source publishes no volume
    /// count. With a count, the count is the limit.
    /// </summary>
    // Shared so the preview's steppers stop exactly where the API would start refusing.
    public const int InitialCountLimitWithoutVolumeCount = 30;
}
