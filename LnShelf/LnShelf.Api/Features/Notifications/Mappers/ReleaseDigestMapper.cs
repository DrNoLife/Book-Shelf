namespace LnShelf.Api.Features.Notifications.Mappers;

/// <summary>
/// Turns a sync's worth of activity into one notification: a count in the title, the first
/// few volumes in the body.
/// </summary>
// One message per pass rather than one per volume (D4): a series that gains three volumes
// overnight is one thing to know, and a lock screen with nine messages on it is not read.
public static class ReleaseDigestMapper
{
    /// <summary>How many volumes the body names before "+N more".</summary>
    public const int NamedInBody = 3;

    /// <summary>The same tag every time, so a later digest replaces an earlier one on the device.</summary>
    public const string Tag = "lnshelf-releases";

    /// <param name="events">At least one, each with its series loaded.</param>
    public static ReleaseNotification ToNotification(IReadOnlyList<ShelfEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count == 0)
        {
            throw new ArgumentException("A digest needs at least one event.", nameof(events));
        }

        // Released volumes first: they are the ones a reader can buy today.
        var ordered = events
            .OrderBy(shelfEvent => shelfEvent.Kind is ShelfEventKind.VolumeReleased ? 0 : 1)
            .ThenBy(shelfEvent => shelfEvent.Series?.TitleEnglish, StringComparer.OrdinalIgnoreCase)
            .ThenBy(shelfEvent => shelfEvent.VolumeNumber)
            .ToList();

        var named = string.Join(", ", ordered.Take(NamedInBody).Select(Describe));
        var more = ordered.Count - NamedInBody;
        var body = more > 0 ? $"{named} +{more} more" : named;

        // A click lands where the news is: the series when there is one, the shelf otherwise.
        var seriesIds = ordered.Select(shelfEvent => shelfEvent.SeriesId).Distinct().ToList();
        var url = seriesIds.Count == 1 ? $"/series/{seriesIds[0]}" : "/";

        return new ReleaseNotification(TitleFor(ordered), body, url, Tag);
    }

    private static string TitleFor(IReadOnlyList<ShelfEvent> events)
    {
        var released = events.Count(shelfEvent => shelfEvent.Kind is ShelfEventKind.VolumeReleased);
        var updates = events.Count - released;

        var releasedText = $"{released} new {Plural(released, "volume", "volumes")} out";
        // "Release update", not "release date update": an announcement without a date is in
        // this group too, and the body says which each one is.
        var updatesText = $"{updates} release {Plural(updates, "update", "updates")}";

        return (released, updates) switch
        {
            (_, 0) => releasedText,
            (0, _) => updatesText,
            _ => $"{releasedText} · {updatesText}",
        };
    }

    // "Vol 18 dated 12 Dec 2026" for a first date: in practice that is how an announcement
    // arrives, because sync only creates volumes the count already says are out.
    private static string Describe(ShelfEvent shelfEvent)
    {
        var volume = $"{shelfEvent.Series?.TitleEnglish ?? "A series"} Vol {shelfEvent.VolumeNumber.ToVolumeText()}";

        return (shelfEvent.Kind, shelfEvent.PreviousDate, shelfEvent.NewDate) switch
        {
            (ShelfEventKind.VolumeReleased, _, _) => volume,
            (ShelfEventKind.VolumeAnnounced, _, { } date) => $"{volume} announced for {DateText(date)}",
            (ShelfEventKind.VolumeAnnounced, _, null) => $"{volume} announced",
            (ShelfEventKind.ReleaseDateChanged, null, { } date) => $"{volume} dated {DateText(date)}",
            (ShelfEventKind.ReleaseDateChanged, _, { } date) => $"{volume} moved to {DateText(date)}",
            _ => volume,
        };
    }

    private static string DateText(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}
