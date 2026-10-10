namespace LnShelf.Client.Features.Library;

/// <summary>
/// A series with something on the shelf to read, and the volume to read next — its id too,
/// so the card can mark it finished without a trip to the series page.
/// </summary>
public sealed record ContinueReadingEntry(LibraryCardResponse Card, int NextVolumeNumber, Guid NextVolumeId)
{
    /// <summary>
    /// The next volume's read status as it stands now — Unread or Reading — which is what an
    /// undo puts back.
    /// </summary>
    public ReadStatus NextVolumeReadStatus =>
        Card.VolumeTicks.FirstOrDefault(tick => tick.Number == NextVolumeNumber)?.ReadStatus ?? ReadStatus.Unread;
}

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
    // The next volume is the server's answer (LibraryCardProjection), not worked out here
    // from the ticks: the card needs its id to mark it read, and two derivations of "next"
    // could disagree about which volume a click marks.
    public static IReadOnlyList<ContinueReadingEntry> ContinueReading(
        IEnumerable<LibraryCardResponse> cards, int take) =>
    [
        .. cards
            .Where(card => card.TrackingStatus != TrackingStatus.Dropped)
            .OrderByDescending(card => card.LastReadAt ?? DateOnly.MinValue)
            .ThenByDescending(card => card.Backlog)
            .Select(card => card is { NextToReadNumber: { } number, NextToReadVolumeId: { } volumeId }
                ? new ContinueReadingEntry(card, number, volumeId)
                : null)
            .OfType<ContinueReadingEntry>()
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

    private static int FirstToBuy(LibraryCardResponse card) =>
        card.VolumeTicks
            .Where(tick => tick.Kind == VolumeTickKind.ToBuy)
            .Select(tick => tick.Number)
            .DefaultIfEmpty(1)
            .Min();
}
