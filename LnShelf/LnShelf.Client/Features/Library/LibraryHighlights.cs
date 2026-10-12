using LnShelf.Client.Features.Series;

namespace LnShelf.Client.Features.Library;

/// <summary>
/// A series on "Continue reading" (and the phone's "Up next"): a volume to finish, or the next
/// chapter, depending on what the reader counts the series in.
/// </summary>
public abstract record ContinueReadingEntry(LibraryCardResponse Card);

/// <summary>
/// A series counted in volumes, with something on the shelf to read, and the volume to read
/// next — its id too, so the card can mark it finished without a trip to the series page.
/// </summary>
public sealed record NextVolumeEntry(LibraryCardResponse Card, decimal NextVolumeNumber, Guid NextVolumeId)
    : ContinueReadingEntry(Card)
{
    /// <summary>
    /// The next volume's read status as it stands now — Unread or Reading — which is what an
    /// undo puts back.
    /// </summary>
    public ReadStatus NextVolumeReadStatus =>
        Card.VolumeTicks.FirstOrDefault(tick => tick.Number == NextVolumeNumber)?.ReadStatus ?? ReadStatus.Unread;
}

/// <summary>A series counted in chapters that the reader is following and has not caught up on.</summary>
public sealed record NextChapterEntry(LibraryCardResponse Card) : ContinueReadingEntry(Card)
{
    /// <summary>What "+1 chapter" records: the same next whole chapter the series page's "+1" does.</summary>
    public decimal NextChapter => ChapterInput.Next(Card.LastChapterRead);
}

/// <summary>A series with volumes out that are not owned, and where the buying starts.</summary>
public sealed record OutNowEntry(LibraryCardResponse Card, decimal FirstVolumeNumber);

/// <summary>
/// The two lists above the shelf. Derived from the cards on every call, never stored:
/// "to buy" and "backlog" fall out of read / owned / released (CLAUDE.md).
/// </summary>
public static class LibraryHighlights
{
    /// <summary>
    /// Series with something to read next, the one read most recently first. A series counted
    /// in volumes needs an owned, unread volume; one counted in chapters needs to be Reading
    /// and not caught up. Dropped series are left out: the user already said they are not
    /// continuing them.
    /// </summary>
    public static IReadOnlyList<ContinueReadingEntry> ContinueReading(
        IEnumerable<LibraryCardResponse> cards, int take) =>
    [
        .. cards
            .Where(card => card.TrackingStatus != TrackingStatus.Dropped)
            .OrderByDescending(card => card.LastReadAt ?? DateOnly.MinValue)
            .ThenByDescending(card => card.Backlog)
            .Select(EntryFor)
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

    // The unit decides the path, and only the unit: a reader who counts a manga in chapters
    // follows it online, so owned volumes say nothing about whether there is more to read, and
    // a caught-up chapter reader with a volume on the shelf has nothing to continue.
    //
    // The next volume is the server's answer (LibraryCardProjection), not worked out here from
    // the ticks: the card needs its id to mark it read, and two derivations of "next" could
    // disagree about which volume a click marks.
    private static ContinueReadingEntry? EntryFor(LibraryCardResponse card) => card switch
    {
        { ProgressUnit: ProgressUnit.Chapters } => IsFollowingInChapters(card) ? new NextChapterEntry(card) : null,
        { NextToReadNumber: { } number, NextToReadVolumeId: { } volumeId } => new NextVolumeEntry(card, number, volumeId),
        _ => null,
    };

    // Reading only: a chapter series on Plan to start or Completed has no "next" the reader
    // asked for, and nothing on the shelf (an owned volume) can stand in for that choice.
    private static bool IsFollowingInChapters(LibraryCardResponse card) =>
        card.TrackingStatus is TrackingStatus.Reading
        && !ChapterProgress.IsCaughtUp(card.LatestChapter, card.LastChapterRead);

    private static decimal FirstToBuy(LibraryCardResponse card) =>
        card.VolumeTicks
            .Where(tick => tick.Kind == VolumeTickKind.ToBuy)
            .Select(tick => tick.Number)
            .DefaultIfEmpty(1)
            .Min();
}
