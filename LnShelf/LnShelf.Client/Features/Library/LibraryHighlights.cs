namespace LnShelf.Client.Features.Library;

/// <summary>A series with something on the shelf to read, and the volume to read next.</summary>
public sealed record ContinueReadingEntry(LibraryCardResponse Card, int NextVolumeNumber);

/// <summary>A series with volumes out that are not owned, and where the buying starts.</summary>
public sealed record OutNowEntry(LibraryCardResponse Card, int FirstVolumeNumber);

/// <summary>
/// The two lists above the shelf. Derived from the cards on every call, never stored:
/// "to buy" and "backlog" fall out of read / owned / released (CLAUDE.md).
/// </summary>
public static class LibraryHighlights
{
    /// <summary>
    /// Series with an owned, unread volume, the one read most recently first. Dropped
    /// series are left out: the user already said they are not continuing them.
    /// </summary>
    public static IReadOnlyList<ContinueReadingEntry> ContinueReading(
        IEnumerable<LibraryCardResponse> cards, int take) =>
    [
        .. cards
            .Where(card => card.Backlog > 0 && card.TrackingStatus != TrackingStatus.Dropped)
            .OrderByDescending(card => card.LastReadAt ?? DateOnly.MinValue)
            .ThenByDescending(card => card.Backlog)
            .Select(card => new ContinueReadingEntry(card, NextUnreadOwned(card)))
            .Where(entry => entry.NextVolumeNumber > 0)
            .Take(take),
    ];

    /// <summary>Series with released volumes not owned; the most behind first.</summary>
    public static IReadOnlyList<OutNowEntry> OutNow(IEnumerable<LibraryCardResponse> cards, int take) =>
    [
        .. cards
            .Where(card => card.ToBuy > 0 && card.TrackingStatus != TrackingStatus.Dropped)
            .OrderByDescending(card => card.ToBuy)
            .ThenBy(card => card.TitleEnglish, StringComparer.OrdinalIgnoreCase)
            .Select(card => new OutNowEntry(card, card.NextToBuyNumber ?? FirstToBuy(card)))
            .Take(take),
    ];

    // Zero means "none found", which the caller filters out rather than showing "Vol 0".
    private static int NextUnreadOwned(LibraryCardResponse card) =>
        card.VolumeTicks
            .Where(tick => tick.Owned && tick.ReadStatus != ReadStatus.Read)
            .Select(tick => tick.Number)
            .DefaultIfEmpty(0)
            .Min();

    private static int FirstToBuy(LibraryCardResponse card) =>
        card.VolumeTicks
            .Where(tick => tick.Kind == VolumeTickKind.ToBuy)
            .Select(tick => tick.Number)
            .DefaultIfEmpty(1)
            .Min();
}
