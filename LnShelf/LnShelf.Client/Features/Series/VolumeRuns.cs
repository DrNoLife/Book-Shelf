namespace LnShelf.Client.Features.Series;

/// <summary>
/// The "mark 1–N" arithmetic, pulled out of the volume section so it can be read — and
/// tested — without a browser. <c>PopoverPlacer</c> is the same move for the same reason.
/// </summary>
public static class VolumeRuns
{
    /// <summary>
    /// One past the end of the leading run that already satisfies the predicate,
    /// capped at what is actually out — offering "mark 1–18 owned" on a series with 16
    /// volumes in print would be offering to own something that does not exist.
    /// </summary>
    /// <returns>Null when every volume in print already satisfies it.</returns>
    public static int? NextRunEnd(
        IEnumerable<VolumeResponse> volumes,
        Func<VolumeResponse, bool> satisfied)
    {
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentNullException.ThrowIfNull(satisfied);

        var released = volumes.Where(volume => volume.IsOut).OrderBy(volume => volume.Number).ToList();
        if (released.Count == 0)
        {
            return null;
        }

        var run = 0;
        foreach (var volume in released)
        {
            if (!satisfied(volume))
            {
                break;
            }

            run++;
        }

        return run >= released.Count ? null : released[run].Number;
    }
}
