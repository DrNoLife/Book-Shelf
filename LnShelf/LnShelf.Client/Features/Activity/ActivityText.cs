using LnShelf.Client.Features.Upcoming;
using LnShelf.Shared.Activity;

namespace LnShelf.Client.Features.Activity;

/// <summary>
/// What "Since your last visit" says about one series: "Vols 13–14 are out", "Vol 18 dated
/// 12 Dec 2026", "Vol 9 moved to 20 Dec 2026".
/// </summary>
public static class ActivityText
{
    /// <summary>The series' news, the releases first: they are what the reader can act on.</summary>
    // Releases share one line because several volumes out at once is one piece of news, and
    // "Vol 12 is out · Vol 13 is out · Vol 14 is out" would read as three.
    public static IReadOnlyList<string> Lines(ActivitySeriesResponse series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var released = series.Events
            .Where(shelfEvent => shelfEvent.Kind is ShelfEventKind.VolumeReleased)
            .Select(shelfEvent => shelfEvent.VolumeNumber)
            .ToList();

        List<string> lines = [];
        if (released.Count > 0)
        {
            var verb = released.Count == 1 ? "is" : "are";
            lines.Add($"{VolumeNumberText.Describe(released)} {verb} out");
        }

        lines.AddRange(series.Events
            .Where(shelfEvent => shelfEvent.Kind is not ShelfEventKind.VolumeReleased)
            .Select(For));

        return lines;
    }

    /// <summary>One event in words.</summary>
    public static string For(ActivityEventResponse shelfEvent)
    {
        ArgumentNullException.ThrowIfNull(shelfEvent);

        var volume = $"Vol {shelfEvent.VolumeNumber.ToVolumeText()}";

        return shelfEvent.Kind switch
        {
            ShelfEventKind.VolumeReleased => $"{volume} is out",
            ShelfEventKind.VolumeAnnounced when shelfEvent.NewDate is { } date => $"{volume} announced for {date.Full}",
            ShelfEventKind.VolumeAnnounced => $"{volume} announced",

            // No previous date is a first date, not a move: in practice how a newly announced
            // volume shows up, because the sync creates main volumes already out.
            ShelfEventKind.ReleaseDateChanged when shelfEvent.NewDate is { } date => shelfEvent.PreviousDate is null
                ? $"{volume} dated {date.Full}"
                : $"{volume} moved to {date.Full}",
            ShelfEventKind.ReleaseDateChanged => $"{volume} has a new date",
            _ => throw new ArgumentOutOfRangeException(nameof(shelfEvent), shelfEvent.Kind, "Unknown shelf event kind."),
        };
    }
}
