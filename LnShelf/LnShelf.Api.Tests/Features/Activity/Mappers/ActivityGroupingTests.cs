using LnShelf.Api.Features.Activity.Mappers;
using LnShelf.Api.Features.Activity.Models;

namespace LnShelf.Api.Tests.Features.Activity.Mappers;

/// <summary>
/// One line per volume, series with the newest news first, and a cap on how much is shown.
/// </summary>
public sealed class ActivityGroupingTests
{
    private static readonly Guid Overlord = Guid.CreateVersion7();
    private static readonly Guid EightySix = Guid.CreateVersion7();
    private static readonly DateTimeOffset Monday = new(2026, 10, 5, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Group_NoEvents_ReturnsNoSeries()
    {
        ActivityGrouping.Group([]).Series.ShouldBeEmpty();
    }

    [Fact]
    public void Group_EventsOnTwoSeries_PutsTheSeriesWithTheNewestEventFirst()
    {
        var response = ActivityGrouping.Group(
        [
            Entry(Overlord, "Overlord", ShelfEventKind.VolumeReleased, 17, Monday),
            Entry(EightySix, "86—EIGHTY-SIX", ShelfEventKind.VolumeReleased, 14, Monday.AddDays(2)),
            Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 18, Monday.AddDays(1)),
        ]);

        response.Series.Select(series => series.SeriesTitle).ShouldBe(["86—EIGHTY-SIX", "Overlord"]);
        response.Series[1].Events.Select(shelfEvent => shelfEvent.VolumeNumber).ShouldBe([18m, 17m]);
    }

    // Dated, then out, within one absence: the reader needs "Vol 9 is out", not its history.
    [Fact]
    public void Group_SeveralEventsOnOneVolume_KeepsOnlyTheNewest()
    {
        var response = ActivityGrouping.Group(
        [
            Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 9, Monday),
            Entry(Overlord, "Overlord", ShelfEventKind.VolumeReleased, 9, Monday.AddDays(3)),
            Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 9, Monday.AddDays(1)),
        ]);

        response.Series.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().Kind.ShouldBe(ShelfEventKind.VolumeReleased);
    }

    // Dated, then moved, within one absence: the reader never saw the first date.
    [Fact]
    public void Group_DatedThenMoved_ReadsAsDatedWithTheNewestDate()
    {
        var response = ActivityGrouping.Group(
        [
            Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 18, Monday) with
            {
                NewDate = new DateOnly(2026, 11, 20),
            },
            Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 18, Monday.AddDays(1)) with
            {
                PreviousDate = new DateOnly(2026, 11, 20),
                NewDate = new DateOnly(2026, 12, 12),
            },
        ]);

        var shelfEvent = response.Series.ShouldHaveSingleItem().Events.ShouldHaveSingleItem();

        shelfEvent.PreviousDate.ShouldBeNull();
        shelfEvent.NewDate.ShouldBe(new DateOnly(2026, 12, 12));
    }

    [Fact]
    public void Group_SomeEvents_ThroughIsTheNewestEventRead()
    {
        var response = ActivityGrouping.Group(
        [
            Entry(Overlord, "Overlord", ShelfEventKind.VolumeReleased, 17, Monday),
            Entry(Overlord, "Overlord", ShelfEventKind.VolumeReleased, 18, Monday.AddDays(2)),
        ]);

        response.Through.ShouldBe(Monday.AddDays(2));
        ActivityGrouping.Group([]).Through.ShouldBeNull();
    }

    [Fact]
    public void Group_MoreVolumesThanTheCap_KeepsTheNewest()
    {
        var entries = Enumerable.Range(1, ActivityGrouping.MaximumEvents + 5)
            .Select(number => Entry(Overlord, "Overlord", ShelfEventKind.VolumeReleased, number, Monday.AddHours(number)))
            .ToList();

        var events = ActivityGrouping.Group(entries).Series.ShouldHaveSingleItem().Events;

        events.Count.ShouldBe(ActivityGrouping.MaximumEvents);
        events[0].VolumeNumber.ShouldBe(ActivityGrouping.MaximumEvents + 5);
        events.ShouldNotContain(shelfEvent => shelfEvent.VolumeNumber <= 5);
    }

    [Fact]
    public void Group_ADateChange_CarriesBothDates()
    {
        var entry = Entry(Overlord, "Overlord", ShelfEventKind.ReleaseDateChanged, 18, Monday) with
        {
            PreviousDate = new DateOnly(2026, 11, 20),
            NewDate = new DateOnly(2026, 12, 12),
        };

        var shelfEvent = ActivityGrouping.Group([entry]).Series.ShouldHaveSingleItem().Events.ShouldHaveSingleItem();

        shelfEvent.PreviousDate.ShouldBe(new DateOnly(2026, 11, 20));
        shelfEvent.NewDate.ShouldBe(new DateOnly(2026, 12, 12));
    }

    private static ActivityEntry Entry(
        Guid seriesId, string title, ShelfEventKind kind, decimal volumeNumber, DateTimeOffset occurredAt) =>
        new(seriesId, title, Format.LightNovel, kind, volumeNumber, PreviousDate: null, NewDate: null, occurredAt);
}
