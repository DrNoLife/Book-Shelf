namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// One metadata source's half of adding a series: which id names it, whether the shelf
/// already holds it, and how to build the row.
/// </summary>
// The seam CLAUDE.md's Open/Closed rule asks for. Everything around it — the idempotency
// read, the save, the ISBN adoption and the refresh that follow — is the same whichever
// catalogue the id came from, and lives once in SeriesCommandService. A third source is a
// new implementation and one registration line: no edit to the pipeline, and no switch in
// a controller quietly deciding what an unrecognised source means.
public interface IAddSeriesFlow
{
    /// <summary>The source this flow adds from; the pipeline looks the flow up by it.</summary>
    MetadataSourceName Source { get; }

    /// <summary>
    /// This source's own id for the request.
    /// </summary>
    // A long for both sources, because MangaUpdates' ids are eleven digits and AniList's
    // fit in one without complaint. Each flow narrows it back to the shape its own column
    // and its own client use.
    /// <exception cref="ArgumentException">The request carries no id for this source.</exception>
    long SourceIdOf(AddSeriesRequest request);

    /// <summary>
    /// Whether a shelf row is already this series.
    /// </summary>
    // One predicate per source rather than one shared: each keys on its own external id
    // column, and the pipeline has no business knowing which column that is.
    Expression<Func<Series, bool>> Identifies(long sourceId);

    /// <summary>
    /// Fetches the series from the source and builds the shelf row, its volumes and its
    /// locally cached cover. Nothing is persisted — the pipeline saves.
    /// </summary>
    /// <returns>Null when the source has no series with that id.</returns>
    Task<Series?> BuildAsync(
        AddSeriesRequest request,
        long sourceId,
        CancellationToken cancellationToken);
}
