using LnShelf.Api.Features.Stats.Mappers;
using static LnShelf.Api.Tests.Features.Releases.Fixtures.UpcomingShelf;

namespace LnShelf.Api.Tests.Features.Stats.Mappers;

/// <summary>
/// The Stats page's figures. Its To buy and Backlog have to be the library's, and its
/// monthly charts have to put every dated volume in the right month and no undated one
/// anywhere.
/// </summary>
public sealed class StatsCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    [Fact]
    public void Calculate_ReadPerMonth_IsTheLastTwelveMonthsEndingWithThisOne()
    {
        var stats = StatsCalculator.Calculate([], Today);

        stats.ReadPerMonth.Count.ShouldBe(12);
        stats.ReadPerMonth[0].Month.ShouldBe(new DateOnly(2025, 10, 1));
        stats.ReadPerMonth[^1].Month.ShouldBe(new DateOnly(2026, 9, 1));
        stats.SpendPerMonth.Select(month => month.Month).ShouldBe(stats.ReadPerMonth.Select(month => month.Month));
    }

    [Fact]
    public void Calculate_ReadVolumes_AreCountedInTheirMonthAndSplitByFormat()
    {
        var overlord = CreateSeries(
            "Overlord",
            Format.LightNovel,
            volumes:
            [
                InPrint(1, owned: true, ReadStatus.Read, readAt: new DateOnly(2026, 9, 2)),
                InPrint(2, owned: true, ReadStatus.Read, readAt: new DateOnly(2026, 9, 20)),
                InPrint(3, owned: true, ReadStatus.Read, readAt: new DateOnly(2026, 8, 31)),
            ]);
        var slimeManga = CreateSeries(
            "That Time I Got Reincarnated as a Slime",
            Format.Manga,
            volumes: [InPrint(1, owned: true, ReadStatus.Read, readAt: new DateOnly(2026, 9, 5))]);

        var stats = StatsCalculator.Calculate([overlord, slimeManga], Today);

        var september = stats.ReadPerMonth[^1];
        september.LightNovel.ShouldBe(2);
        september.Manga.ShouldBe(1);
        september.Total.ShouldBe(3);
        stats.ReadPerMonth[^2].LightNovel.ShouldBe(1);
        stats.ReadWithoutDate.ShouldBe(0);
    }

    [Fact]
    public void Calculate_AReadVolumeWithNoDate_IsCountedAsUndatedAndChartedNowhere()
    {
        // The real shape of an import: read volumes whose export row carried no date.
        var eightySix = CreateSeries("86—EIGHTY-SIX", volumes: [InPrint(1, owned: true, ReadStatus.Read)]);

        var stats = StatsCalculator.Calculate([eightySix], Today);

        stats.ReadWithoutDate.ShouldBe(1);
        stats.ReadPerMonth.Sum(month => month.Total).ShouldBe(0);
        stats.Formats.ShouldHaveSingleItem().Read.ShouldBe(1);
    }

    [Fact]
    public void Calculate_Formats_UseTheLibrarysToBuyAndBacklogRules()
    {
        // 1 read, 2 owned-unread (one of them Reading — still backlog), 1 in print and
        // unbought, 1 announced (neither to buy nor backlog).
        var torturePrincess = CreateSeries(
            "Torture Princess: Fremd Torturchen",
            volumes:
            [
                InPrint(1, owned: true, ReadStatus.Read),
                InPrint(2, owned: true, ReadStatus.Reading),
                InPrint(3, owned: true),
                InPrint(4),
                NotOut(5),
            ]);

        var format = StatsCalculator.Calculate([torturePrincess], Today).Formats.ShouldHaveSingleItem();

        format.Format.ShouldBe(Format.LightNovel);
        format.SeriesCount.ShouldBe(1);
        format.Read.ShouldBe(1);
        format.Owned.ShouldBe(3);
        format.Backlog.ShouldBe(2);
        format.ToBuy.ShouldBe(1);
    }

    [Fact]
    public void Calculate_Spend_AveragesOverPricedVolumesAndChartsOnlyDatedOnes()
    {
        var overlord = CreateSeries(
            "Overlord",
            volumes:
            [
                InPrint(1, owned: true, price: 150m, purchasedAt: new DateOnly(2026, 9, 3)),
                InPrint(2, owned: true, price: 100m, purchasedAt: new DateOnly(2026, 9, 10)),
                InPrint(3, owned: true, price: 125m),
                InPrint(4, owned: true),
            ]);

        var stats = StatsCalculator.Calculate([overlord], Today);

        var spend = stats.Spend.ShouldNotBeNull();
        spend.PricedCount.ShouldBe(3);
        spend.Total.ShouldBe(375m);
        spend.Average.ShouldBe(125m);

        stats.SpendPerMonth[^1].Total.ShouldBe(250m);
        stats.SpendPerMonth[^1].VolumeCount.ShouldBe(2);
        stats.SpendPerMonth.Sum(month => month.Total).ShouldBe(250m);
    }

    [Fact]
    public void Calculate_NothingPriced_HasNoSpendBlock()
    {
        var overlord = CreateSeries("Overlord", volumes: [InPrint(1, owned: true)]);

        StatsCalculator.Calculate([overlord], Today).Spend.ShouldBeNull();
    }

    [Fact]
    public void Calculate_Completion_CountsStartedAsEverythingButPlanToStart()
    {
        var shelf = new[]
        {
            CreateSeries("Overlord", trackingStatus: TrackingStatus.Completed),
            CreateSeries("86—EIGHTY-SIX", trackingStatus: TrackingStatus.Reading),
            CreateSeries("Torture Princess: Fremd Torturchen", trackingStatus: TrackingStatus.Dropped),
            CreateSeries("That Time I Got Reincarnated as a Slime", trackingStatus: TrackingStatus.PlanToStart),

            // No user-state row reads as Plan to start, as it does on the library card.
            CreateSeries("Unshelved", trackingStatus: null),
        };

        var stats = StatsCalculator.Calculate(shelf, Today);

        stats.CompletedSeries.ShouldBe(1);
        stats.StartedSeries.ShouldBe(3);
    }

    [Fact]
    public void Calculate_LongestSeries_IsMostReadFirstAndSkipsUnreadSeries()
    {
        var overlord = CreateSeries("Overlord", volumes: [.. Enumerable.Range(1, 3).Select(number => InPrint(number, owned: true, ReadStatus.Read))]);
        var slime = CreateSeries("That Time I Got Reincarnated as a Slime", volumes: [.. Enumerable.Range(1, 5).Select(number => InPrint(number, owned: true, ReadStatus.Read)), InPrint(6)]);
        var eightySix = CreateSeries("86—EIGHTY-SIX", volumes: [InPrint(1, owned: true)]);

        var longest = StatsCalculator.Calculate([overlord, slime, eightySix], Today).LongestSeries;

        longest.Select(entry => entry.Title).ShouldBe(["That Time I Got Reincarnated as a Slime", "Overlord"]);
        longest[0].Read.ShouldBe(5);
        longest[0].Tracked.ShouldBe(6);
        longest[0].Owned.ShouldBe(5);
        longest[0].Out.ShouldBe(6);
    }

    [Fact]
    public void Calculate_FirstReadAt_IsTheEarliestReadDateAndNullWhenNoneIsDated()
    {
        var overlord = CreateSeries(
            "Overlord",
            volumes:
            [
                InPrint(1, owned: true, ReadStatus.Read, readAt: new DateOnly(2024, 3, 9)),
                InPrint(2, owned: true, ReadStatus.Read, readAt: new DateOnly(2021, 10, 2)),
                InPrint(3, owned: true, ReadStatus.Read),
            ]);

        StatsCalculator.Calculate([overlord], Today).FirstReadAt.ShouldBe(new DateOnly(2021, 10, 2));
        StatsCalculator.Calculate([CreateSeries("Undated", volumes: [InPrint(1, owned: true, ReadStatus.Read)])], Today)
            .FirstReadAt.ShouldBeNull();
        StatsCalculator.Calculate([], Today).FirstReadAt.ShouldBeNull();
    }

    [Fact]
    public void Calculate_RecentlyRead_IsLatestFirstCappedAtFiveAndSkipsUndatedAndUnread()
    {
        var overlord = CreateSeries(
            "Overlord",
            volumes:
            [
                .. Enumerable.Range(1, 6).Select(number =>
                    InPrint(number, owned: true, ReadStatus.Read, readAt: new DateOnly(2026, 9, number))),
                InPrint(7, owned: true, ReadStatus.Read),
                InPrint(8, owned: true, ReadStatus.Unread, readAt: new DateOnly(2026, 9, 28)),
            ]);

        var recent = StatsCalculator.Calculate([overlord], Today).RecentlyRead;

        recent.Select(read => read.VolumeNumber).ShouldBe([6, 5, 4, 3, 2]);
        recent[0].SeriesTitle.ShouldBe("Overlord");
        recent[0].ReadAt.ShouldBe(new DateOnly(2026, 9, 6));
    }
}
