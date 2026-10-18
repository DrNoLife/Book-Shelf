namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// The progress unit a series that is being created starts with: the reader's preference where
/// the format offers chapters, Volumes everywhere else.
/// </summary>
// One rule for the three creation paths (add from a source, add by hand, import), so they
// cannot drift apart. It is applied to a series that has not been saved yet and never to one
// that already exists: adopting or matching a tracked series must leave its unit as the reader
// set it. The format is read from the built series, not the request, because a source can
// answer with a different format than the one asked for.
public static class NewSeriesProgressUnit
{
    public static void ApplyTo(Series series, ProgressUnit preferredUnit)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (!ChapterProgress.IsOfferedFor(series.Format))
        {
            return;
        }

        series.UserState ??= SeriesUserState.Untouched(series.Id);
        series.UserState.ProgressUnit = preferredUnit;
    }
}
