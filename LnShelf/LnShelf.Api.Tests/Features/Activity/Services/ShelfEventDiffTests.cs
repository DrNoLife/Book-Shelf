using LnShelf.Api.Features.Activity.Models;
using LnShelf.Api.Features.Activity.Services;

namespace LnShelf.Api.Tests.Features.Activity.Services;

/// <summary>
/// What counts as news between two syncs of one series: a volume coming out (by count or by
/// date, once), a volume past the end of what was known, and a not-yet-out volume's new date.
/// </summary>
public sealed class ShelfEventDiffTests
{
    private static readonly DateOnly PreviousSyncDay = new(2026, 10, 9);
    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Capture_ASeriesNeverSynced_IsNull()
    {
        // Its first refresh brings the whole backlist, and none of that is news.
        var series = SeriesWith(synced: false);

        ShelfEventDiff.Capture(series, TimeZoneInfo.Utc).ShouldBeNull();
    }

    [Fact]
    public void Compare_TheCountMarksAVolumeOut_IsVolumeReleased()
    {
        var series = SeriesWith(volumes: [Volume(1, isReleased: true), Volume(2)]);
        var before = Capture(series);

        series.Volumes.Single(volume => volume.Number == 2).IsReleased = true;

        var shelfEvent = Compare(before, series).ShouldHaveSingleItem();
        shelfEvent.Kind.ShouldBe(ShelfEventKind.VolumeReleased);
        shelfEvent.VolumeNumber.ShouldBe(2);
        shelfEvent.SeriesId.ShouldBe(series.Id);
        shelfEvent.OccurredAt.ShouldBe(Now);
        shelfEvent.NotifiedAt.ShouldBeNull();
    }

    // The case a flip-based event got wrong: the date passes days before MangaUpdates counts
    // the volume, so it is news on the day it comes out, and the later flip is not news again.
    [Fact]
    public void Compare_ADatedVolumeWhoseDayHasCome_IsReleasedOnceAndNotAgainWhenTheCountCatchesUp()
    {
        var series = SeriesWith(volumes: [Volume(1, isReleased: true), Volume(2, date: new DateOnly(2026, 10, 10))]);

        var released = Compare(Capture(series), series).ShouldHaveSingleItem();
        released.Kind.ShouldBe(ShelfEventKind.VolumeReleased);
        released.NewDate.ShouldBe(new DateOnly(2026, 10, 10));

        // The next night: synced on the 11th, and the count now says the same thing.
        series.LastSyncedAt = Now;
        var nextNight = Capture(series);
        series.Volumes.Single(volume => volume.Number == 2).IsReleased = true;

        Compare(nextNight, series, today: Today.AddDays(1)).ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AVolumeAlreadyOutByDateAtTheLastSync_IsNotNews()
    {
        var series = SeriesWith(volumes: [Volume(1, date: PreviousSyncDay)]);
        var before = Capture(series);

        series.Volumes.Single().IsReleased = true;

        Compare(before, series).ShouldBeEmpty();
    }

    public static TheoryData<bool, ShelfEventKind> NewVolumesPastTheEnd => new()
    {
        { true, ShelfEventKind.VolumeReleased },
        { false, ShelfEventKind.VolumeAnnounced },
    };

    [Theory]
    [MemberData(nameof(NewVolumesPastTheEnd))]
    public void Compare_ANewVolumePastTheOldCountAndHighestNumber_IsNews(bool isReleased, ShelfEventKind expected)
    {
        var series = SeriesWith(volumes: [Volume(1, isReleased: true), Volume(2, isReleased: true)], releaseSourceCount: 2);
        var before = Capture(series);

        series.Volumes.Add(Volume(3, isReleased: isReleased));

        Compare(before, series).ShouldHaveSingleItem().Kind.ShouldBe(expected);
    }

    // A gap an import left (1 and 4 of a ten-volume series) and a bonus volume the catalogue
    // found between two tracked ones are new to the shelf, not to the world.
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(2.5)]
    public void Compare_ANewVolumeBelowWhatWasKnown_IsNotNews(double number)
    {
        var series = SeriesWith(
            volumes: [Volume(2, isReleased: true), Volume(3, isReleased: true), Volume(5, isReleased: true)],
            releaseSourceCount: 5);
        var before = Capture(series);

        series.Volumes.Add(Volume((decimal)number, isReleased: true));

        Compare(before, series).ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ANotYetOutVolumeGetsItsFirstDate_IsADateChangeWithNoPreviousDate()
    {
        // How "Overlord Vol 18 announced" arrives in practice: the row already exists, and
        // gains a date.
        var series = SeriesWith(volumes: [Volume(18)]);
        var before = Capture(series);

        series.Volumes.Single().ReleaseDate = new DateOnly(2026, 12, 12);

        var shelfEvent = Compare(before, series).ShouldHaveSingleItem();
        shelfEvent.Kind.ShouldBe(ShelfEventKind.ReleaseDateChanged);
        shelfEvent.PreviousDate.ShouldBeNull();
        shelfEvent.NewDate.ShouldBe(new DateOnly(2026, 12, 12));
    }

    [Fact]
    public void Compare_ANotYetOutVolumeMoves_CarriesBothDates()
    {
        var series = SeriesWith(volumes: [Volume(9, date: new DateOnly(2026, 11, 1))]);
        var before = Capture(series);

        series.Volumes.Single().ReleaseDate = new DateOnly(2026, 12, 12);

        var shelfEvent = Compare(before, series).ShouldHaveSingleItem();
        shelfEvent.Kind.ShouldBe(ShelfEventKind.ReleaseDateChanged);
        shelfEvent.PreviousDate.ShouldBe(new DateOnly(2026, 11, 1));
        shelfEvent.NewDate.ShouldBe(new DateOnly(2026, 12, 12));
    }

    [Fact]
    public void Compare_ADateThatDisappears_IsNotNews()
    {
        var series = SeriesWith(volumes: [Volume(9, date: new DateOnly(2026, 11, 1))]);
        var before = Capture(series);

        series.Volumes.Single().ReleaseDate = null;

        Compare(before, series).ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ADateMovedIntoThePast_IsReleasedNotADateChange()
    {
        var series = SeriesWith(volumes: [Volume(9, date: new DateOnly(2026, 11, 1))]);
        var before = Capture(series);

        series.Volumes.Single().ReleaseDate = Today;

        Compare(before, series).ShouldHaveSingleItem().Kind.ShouldBe(ShelfEventKind.VolumeReleased);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(17992364412L)]
    public void Compare_LinkedToADifferentRecordDuringTheRefresh_IsNotNews(long? previousLink)
    {
        // A wrong 3-volume record replaced by the right 14-volume one: 4 to 14 are new to the
        // shelf, not to the world.
        var series = SeriesWith(volumes: [Volume(1, isReleased: true), Volume(2, isReleased: true), Volume(3, isReleased: true)], releaseSourceCount: 3);
        series.MangaUpdatesId = previousLink;
        var before = Capture(series);

        series.MangaUpdatesId = 55665566L;
        series.ReleaseSourceVolumeCount = 14;
        for (var number = 4; number <= 14; number++)
        {
            series.Volumes.Add(Volume(number, isReleased: true));
        }

        Compare(before, series).ShouldBeEmpty();
    }

    [Fact]
    public void Compare_NothingChanged_IsEmpty()
    {
        // Idempotency (testing.md #3): the second run of the same sync says nothing.
        var series = SeriesWith(volumes: [Volume(1, isReleased: true), Volume(2, date: new DateOnly(2026, 12, 1))]);

        Compare(Capture(series), series).ShouldBeEmpty();
    }

    private static ShelfSnapshot Capture(Series series) =>
        ShelfEventDiff.Capture(series, TimeZoneInfo.Utc).ShouldNotBeNull();

    private static IReadOnlyList<ShelfEvent> Compare(ShelfSnapshot before, Series after, DateOnly? today = null) =>
        ShelfEventDiff.Compare(before, after, today ?? Today, Now);

    private static Series SeriesWith(
        Volume[]? volumes = null,
        int? releaseSourceCount = null,
        bool synced = true)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Overlord",
            LastSyncedAt = synced
                ? new DateTimeOffset(PreviousSyncDay.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero)
                : null,
            ReleaseSourceVolumeCount = releaseSourceCount,
        };

        foreach (var volume in volumes ?? [])
        {
            series.Volumes.Add(volume);
        }

        return series;
    }

    private static Volume Volume(decimal number, bool isReleased = false, DateOnly? date = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Number = number,
        IsReleased = isReleased,
        ReleaseDate = date,
    };
}
