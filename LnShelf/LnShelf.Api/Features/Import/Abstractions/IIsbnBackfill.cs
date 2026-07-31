namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// Carries ISBNs the import already parsed onto the volumes they identify.
/// </summary>
// Behind an abstraction because the Shelf slice calls it: adding a series adopts whatever
// ISBNs staging already holds for it, so per-volume enrichment has a key to work with in
// the same request. Without that, adding a series the shelf has imported before enriches
// nothing at all — the ISBNs live on volume rows, and a re-added series has new ones.
public interface IIsbnBackfill
{
    /// <summary>Every committed import group, for the one-shot command.</summary>
    Task<IsbnBackfillReport> RunAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Just this series, for the moment it is added.
    /// </summary>
    // Idempotent and additive, exactly like the whole-shelf run: it only ever fills a null.
    Task<IsbnBackfillReport> RunForSeriesAsync(Guid seriesId, CancellationToken cancellationToken);
}
