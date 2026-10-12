namespace LnShelf.Client.Features.Series;

/// <summary>
/// What select mode's "Mark N owned" says, and whether it has anything left to do, for the
/// edition chosen beside it.
/// </summary>
// Pulled out of the toolbar so the wording and the "already done" rule can be tested without
// a browser, the move VolumeRuns already makes for "Mark 1–N".
public static class OwnedMarkText
{
    /// <summary>
    /// Whether every one of these volumes is already owned in this edition, so marking them
    /// would change nothing.
    /// </summary>
    // An owned volume with no recorded edition counts as physical: that is what the API
    // writes for one marked owned without an edition, and what the volume panel shows.
    public static bool AreAllOwnedAs(IReadOnlyCollection<VolumeResponse> volumes, OwnedFormat format)
    {
        ArgumentNullException.ThrowIfNull(volumes);

        return volumes.Count > 0
            && volumes.All(volume => volume.Owned && (volume.OwnedFormat ?? OwnedFormat.Physical) == format);
    }

    /// <summary>
    /// "Mark 3 owned · Digital", or, when there is nothing to change, "All 3 owned as
    /// Digital" — a disabled button that says why rather than one that looks broken.
    /// </summary>
    public static string Label(int selectedCount, bool allOwnedAlready, OwnedFormat format) =>
        (allOwnedAlready, selectedCount) switch
        {
            (true, 1) => $"Already owned as {format}",
            (true, _) => $"All {selectedCount} owned as {format}",
            _ => $"Mark {selectedCount} owned · {format}",
        };
}
