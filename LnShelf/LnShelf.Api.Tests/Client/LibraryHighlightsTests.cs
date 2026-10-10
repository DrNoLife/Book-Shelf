using LnShelf.Client.Features.Library;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The two lists above the library shelf, both derived from the cards on every render.
/// </summary>
public sealed class LibraryHighlightsTests
{
    [Fact]
    public void ContinueReading_PicksTheLowestOwnedUnreadVolume()
    {
        var card = Card(
            "Overlord",
            backlog: 2,
            ticks:
            [
                Tick(1, owned: true, ReadStatus.Read),
                Tick(2, owned: false, ReadStatus.Unread),
                Tick(3, owned: true, ReadStatus.Unread),
                Tick(4, owned: true, ReadStatus.Unread),
            ]);

        var entries = LibraryHighlights.ContinueReading([card], take: 3);

        entries.ShouldHaveSingleItem().NextVolumeNumber.ShouldBe(3);
    }

    [Fact]
    public void ContinueReading_LeavesOutDroppedSeriesAndSeriesWithNothingUnread()
    {
        var dropped = Card("Dropped", backlog: 1, status: TrackingStatus.Dropped, ticks: [Tick(1, owned: true, ReadStatus.Unread)]);
        var caughtUp = Card("Caught up", ticks: [Tick(1, owned: true, ReadStatus.Read)]);

        LibraryHighlights.ContinueReading([dropped, caughtUp], take: 3).ShouldBeEmpty();
    }

    [Fact]
    public void ContinueReading_PutsTheMostRecentlyReadFirst_AndTakesOnlyTheRequestedCount()
    {
        IReadOnlyList<VolumeTickResponse> unread = [Tick(1, owned: true, ReadStatus.Unread)];
        var older = Card("Older", backlog: 1, lastReadAt: new DateOnly(2026, 1, 1), ticks: unread);
        var newer = Card("Newer", backlog: 1, lastReadAt: new DateOnly(2026, 6, 1), ticks: unread);
        var never = Card("Never", backlog: 1, ticks: unread);

        var entries = LibraryHighlights.ContinueReading([older, never, newer], take: 2);

        entries.Select(entry => entry.Card.TitleEnglish).ShouldBe(["Newer", "Older"]);
    }

    [Fact]
    public void OutNow_ListsSeriesWithUnownedReleasedVolumes_MostBehindFirst()
    {
        var slightlyBehind = Card("Slightly", toBuy: 1, nextToBuy: 7);
        var farBehind = Card("Far", toBuy: 5, nextToBuy: 3);
        var caughtUp = Card("Caught up");

        var entries = LibraryHighlights.OutNow([slightlyBehind, caughtUp, farBehind], take: 8);

        entries.Select(entry => (entry.Card.TitleEnglish, entry.FirstVolumeNumber))
            .ShouldBe([("Far", 3), ("Slightly", 7)]);
    }

    [Fact]
    public void OutNow_LeavesOutDroppedSeries() =>
        LibraryHighlights.OutNow([Card("Dropped", toBuy: 3, status: TrackingStatus.Dropped)], take: 8).ShouldBeEmpty();

    [Fact]
    public void OutNow_NoServerHintForTheNextVolume_FallsBackToTheFirstToBuyTick()
    {
        var card = Card(
            "Hintless",
            toBuy: 2,
            ticks:
            [
                Tick(1, owned: true, ReadStatus.Read),
                Tick(4, owned: false, ReadStatus.Unread),
                Tick(6, owned: false, ReadStatus.Unread),
            ]);

        LibraryHighlights.OutNow([card], take: 8).ShouldHaveSingleItem().FirstVolumeNumber.ShouldBe(4);
    }

    private static VolumeTickResponse Tick(int number, bool owned, ReadStatus readStatus) =>
        new() { Number = number, IsOut = true, Owned = owned, ReadStatus = readStatus };

    private static LibraryCardResponse Card(
        string title,
        int backlog = 0,
        int toBuy = 0,
        int? nextToBuy = null,
        TrackingStatus status = TrackingStatus.Reading,
        DateOnly? lastReadAt = null,
        IReadOnlyList<VolumeTickResponse>? ticks = null) =>
        new()
        {
            SeriesId = Guid.NewGuid(),
            TitleEnglish = title,
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = status,
            HasNewVolume = false,
            Read = 0,
            Owned = 0,
            Published = 0,
            ToBuy = toBuy,
            Backlog = backlog,
            Tracked = ticks?.Count ?? 0,
            NextToBuyNumber = nextToBuy,
            LastReadAt = lastReadAt,
            VolumeTicks = ticks ?? [],
        };
}
