namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// The shelf row a newly built series already is, when that row was added from the
/// other catalogue and so carries no id this one would recognise.
/// </summary>
// The duplicate this exists to prevent: a series added through the MangaUpdates fallback
// has no AniList id, and nothing ever goes looking for one (SeriesMetadataMapper). Adding
// or importing the same series through AniList later found nothing under its AniList id,
// built a second row, and split the user's volume state across the two. The reverse —
// an AniList-added series the nightly job has not linked yet, then added from
// MangaUpdates — is the same hole from the other side, and the same check closes it.
//
// Deliberately conservative, because adopting the wrong row would merge two series'
// user state, which is far harder to undo than a duplicate is to delete:
//
// - Same Format only. The light novel and the manga of one title are two series
//   (CLAUDE.md), and a title match across formats is exactly that pair.
// - Only rows that are unlinked on the side the new series is linked on. A row that
//   already has a different id for the same catalogue is provably another series.
// - Exact title equality after TitleNormalizer — the same folding the import grouping
//   uses — against the English and romaji titles on both sides. No fuzzy scoring: a
//   near miss creates a duplicate, which is visible and cheap, never a silent merge.
// - Exactly one such row. Two candidates is ambiguity, and ambiguity creates.
//
// Static over the context, the way DesiredVolumeStateMerger is a static rule: it holds no
// state, and both callers already own the DbContext it reads.
public static class UnlinkedSeriesMatch
{
    /// <param name="built">The series as its source describes it, not yet saved.</param>
    /// <returns>
    /// The existing row, tracked and loaded with its volumes and all user state, or null
    /// when there is no single unambiguous match.
    /// </returns>
    public static async Task<Series?> FindAsync(
        LnShelfDbContext dbContext,
        Series built,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(built);

        var builtTitles = NormalizedTitlesOf(built.TitleEnglish, built.TitleRomaji);
        if (builtTitles.Count is 0)
        {
            return null;
        }

        // Evaluated here so the query compares plain values, not a captured entity's members.
        var format = built.Format;
        var builtHasAniList = built.AniListId is not null;
        var builtHasMangaUpdates = built.MangaUpdatesId is not null;

        // Titles only, and untracked: normalisation has to run here rather than in SQL, and
        // loading every candidate's volumes just to compare names would be the N+1 the
        // library grid is careful to avoid.
        var candidates = await dbContext.Series
            .AsNoTracking()
            .Where(series => series.Format == format
                             && (!builtHasAniList || series.AniListId == null)
                             && (!builtHasMangaUpdates || series.MangaUpdatesId == null))
            .Select(series => new { series.Id, series.TitleEnglish, series.TitleRomaji })
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Where(candidate => NormalizedTitlesOf(candidate.TitleEnglish, candidate.TitleRomaji)
                .Overlaps(builtTitles))
            .Select(candidate => candidate.Id)
            .ToList();

        if (matches.Count is not 1)
        {
            return null;
        }

        var existing = await dbContext.Series
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .Include(series => series.UserState)
            .FirstAsync(series => series.Id == matches[0], cancellationToken);

        // Checked again on the tracked instance: within one import run, an earlier series
        // may already have adopted this row in memory without the database knowing yet.
        return IsUnlinkedFor(existing, built) ? existing : null;
    }

    /// <summary>
    /// Links an existing row to the catalogue the new series came from.
    /// </summary>
    // Only the ids. The row's metadata and every piece of user state stay exactly as they
    // are — the same restraint the import writer shows a series it finds already tracked.
    // What the new id buys is identity: the next add or import of this series finds the
    // row by id instead of by title.
    public static void Adopt(Series existing, Series built)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(built);

        existing.AniListId ??= built.AniListId;
        existing.MangaUpdatesId ??= built.MangaUpdatesId;

        // A series kept by hand stops being so once a source is linked: the sync owns its
        // count from here on, and "Manual" would wrongly say the reader typed it. Cleared
        // rather than guessed; the next count the sync writes fills it in.
        if (existing.VolumeCountBasis == VolumeCountBasis.Manual)
        {
            existing.VolumeCountBasis = null;
            existing.VolumeCountEvidence = null;
        }
    }

    private static bool IsUnlinkedFor(Series existing, Series built) =>
        (built.AniListId is null || existing.AniListId is null)
        && (built.MangaUpdatesId is null || existing.MangaUpdatesId is null);

    private static HashSet<string> NormalizedTitlesOf(params string?[] titles) =>
        [.. titles.Select(TitleNormalizer.Normalize).Where(title => title.Length > 0)];
}
