namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// The named seam for importing someone else's library export (CLAUDE.md —
/// seam-first design). StoryGraph and Goodreads CSV are the implementations; a
/// MyAnimeList export is a third one, not an edit to either.
/// </summary>
// Every implementation is registered under this interface and none of them is
// "the" importer: ImportSourceResolver asks each one whether it recognises the
// file that was uploaded. That is what makes a new source a new class and
// nothing else — the alternative, a source name posted by the client, would put
// the knowledge of which sources exist in the browser bundle.
public interface IShelfImporter
{
    /// <summary>What this importer reads, named for the UI.</summary>
    string SourceName { get; }

    /// <summary>
    /// Whether the uploaded file's header row is this source's export.
    /// </summary>
    /// <param name="headerLine">
    /// The first line of the file, byte-order mark already removed. Never null;
    /// empty when the file has no content at all.
    /// </param>
    // Sniffing rather than a posted source name: an export is identified by what
    // it contains, so a mislabelled upload cannot be parsed by the wrong reader.
    // Each implementation names the columns only its own source writes.
    bool CanRead(string headerLine);

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
