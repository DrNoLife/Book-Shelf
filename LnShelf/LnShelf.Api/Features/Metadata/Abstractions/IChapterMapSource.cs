namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>
/// The named seam for "which chapters are in which volume of this series?" (CLAUDE.md —
/// seam-first design). MangaDex is the first implementation.
/// </summary>
// Its own seam, because none of the others can be asked this: the release source knows a
// latest chapter but not where volumes break, and the per-volume seams know editions, not
// chapters. Several implementations are expected — every source has coverage gaps — so they
// are registered in precedence order and ChapterMapRefresher fills each volume from the first
// source that maps it.
//
// An implementation answers with its raw per-volume chapter lists and nothing more. Turning
// lists into ranges, dropping strays and refusing a map that does not fit the shelf are the
// shelf's rules (ChapterMapBuilder), kept in one place rather than once per source.
public interface IChapterMapSource
{
    /// <summary>Recorded on each volume whose range this source supplied, and named in the credit line.</summary>
    MetadataSourceName SourceName { get; }

    /// <summary>
    /// The source's chapters per volume for this series.
    /// </summary>
    /// <remarks>
    /// May remember its own handle for the series on it (MangaDex: <see cref="Series.MangaDexId"/>)
    /// so the next check skips the title search, exactly as the release source's link is kept on
    /// <see cref="Series.MangaUpdatesId"/>. It writes nothing else: no range, no status.
    /// </remarks>
    Task<ChapterMapAnswer> GetChapterMapAsync(Series series, CancellationToken cancellationToken);
}
