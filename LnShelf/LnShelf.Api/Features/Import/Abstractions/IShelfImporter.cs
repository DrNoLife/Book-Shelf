namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// The named seam for importing someone else's library export (CLAUDE.md —
/// seam-first design). StoryGraph CSV is the first implementation; a Goodreads or
/// MyAnimeList export is a new implementation, not an edit.
/// </summary>
public interface IShelfImporter
{
    /// <summary>What this importer reads, named for the UI.</summary>
    string SourceName { get; }

    /// <summary>
    /// Parses an export, groups its books by series, matches each group against
    /// the metadata source and stages the result. Writes nothing to the shelf —
    /// that is the commit step's job.
    /// </summary>
    Task<ImportBatchResponse> CreateBatchAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken);
}
