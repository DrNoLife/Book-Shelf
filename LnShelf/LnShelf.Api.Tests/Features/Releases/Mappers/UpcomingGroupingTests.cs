using LnShelf.Api.Features.Releases.Mappers;
using LnShelf.Shared.Releases;
using static LnShelf.Api.Tests.Features.Releases.Fixtures.UpcomingShelf;

namespace LnShelf.Api.Tests.Features.Releases.Mappers;

/// <summary>
/// The Upcoming view's groups. "Out" has to mean what it means on the library card, and
/// every dated volume has to land in exactly one month bucket.
/// </summary>
public sealed class UpcomingGroupingTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    [Fact]
    public void Group_ReleasedVolumesNotOwned_AreOneOutNowLinePerSeries()
    {
        // Overlord: 1–14 owned, 15 and 16 in print and unbought.
        var overlord = CreateSeries(
            "Overlord",
            volumes: [.. Enumerable.Range(1, 14).Select(number => InPrint(number, owned: true)), InPrint(15), InPrint(16)]);

        var upcoming = UpcomingGrouping.Group([overlord], Today);

        var line = upcoming.OutNow.ShouldHaveSingleItem();
        line.SeriesTitle.ShouldBe("Overlord");
        line.VolumeNumbers.ShouldBe([15, 16]);
        line.ReleaseDate.ShouldBeNull();
    }

    [Fact]
    public void Group_AnOwnedVolume_IsNeverUpcoming()
    {
        // Owning a volume is one of the three grounds for "out", so a pre-order the reader
        // has marked owned is not something still to buy, whatever its date says.
        var slime = CreateSeries("That Time I Got Reincarnated as a Slime", volumes: [InPrint(1, owned: true)]);

        var upcoming = UpcomingGrouping.Group([slime], Today);

        upcoming.OutNow.ShouldBeEmpty();
        upcoming.Announced.ShouldBeEmpty();
    }

    public static TheoryData<DateOnly, string> DatedBuckets => new()
    {
        // Later this month, the last day of it, then the first and last day of the
        // three months after, then the day after that.
        { new DateOnly(2026, 9, 30), nameof(UpcomingResponse.ThisMonth) },
        { new DateOnly(2026, 10, 1), nameof(UpcomingResponse.NextThreeMonths) },
        { new DateOnly(2026, 12, 31), nameof(UpcomingResponse.NextThreeMonths) },
        { new DateOnly(2027, 1, 1), nameof(UpcomingResponse.Later) },
    };

    [Theory]
    [MemberData(nameof(DatedBuckets))]
    public void Group_AFutureDate_LandsInExactlyOneMonthBucket(DateOnly releaseDate, string expectedBucket)
    {
        var torturePrincess = CreateSeries(
            "Torture Princess: Fremd Torturchen",
            volumes: [InPrint(1, owned: true), NotOut(2, releaseDate, title: "The Second Volume")]);

        var upcoming = UpcomingGrouping.Group([torturePrincess], Today);

        var buckets = new Dictionary<string, IReadOnlyList<UpcomingEntryResponse>>
        {
            [nameof(UpcomingResponse.ThisMonth)] = upcoming.ThisMonth,
            [nameof(UpcomingResponse.NextThreeMonths)] = upcoming.NextThreeMonths,
            [nameof(UpcomingResponse.Later)] = upcoming.Later,
        };

        foreach (var (name, entries) in buckets)
        {
            entries.Count.ShouldBe(name == expectedBucket ? 1 : 0, name);
        }

        var line = buckets[expectedBucket].ShouldHaveSingleItem();
        line.VolumeNumbers.ShouldBe([2]);
        line.ReleaseDate.ShouldBe(releaseDate);
        line.VolumeTitle.ShouldBe("The Second Volume");
        upcoming.Announced.ShouldBeEmpty();
    }

    [Fact]
    public void Group_UndatedVolumesNotOut_AreOneAnnouncedLinePerSeries()
    {
        var eightySix = CreateSeries("86—EIGHTY-SIX", volumes: [InPrint(1, owned: true), NotOut(14), NotOut(15)]);

        var upcoming = UpcomingGrouping.Group([eightySix], Today);

        upcoming.Announced.ShouldHaveSingleItem().VolumeNumbers.ShouldBe([14, 15]);
        upcoming.OutNow.ShouldBeEmpty();
    }

    [Fact]
    public void Group_ADatedVolumeInThePast_IsOutNowNotDated()
    {
        var eightySix = CreateSeries("86—EIGHTY-SIX", volumes: [NotOut(13, new DateOnly(2026, 9, 1))]);

        var upcoming = UpcomingGrouping.Group([eightySix], Today);

        upcoming.OutNow.ShouldHaveSingleItem().VolumeNumbers.ShouldBe([13]);
        upcoming.ThisMonth.ShouldBeEmpty();
    }

    [Fact]
    public void Group_OutNow_PutsTheSeriesMostBehindFirst()
    {
        var overlord = CreateSeries("Overlord", volumes: [InPrint(1)]);
        var slime = CreateSeries("That Time I Got Reincarnated as a Slime", volumes: [InPrint(1), InPrint(2), InPrint(3)]);
        var eightySix = CreateSeries("86—EIGHTY-SIX", volumes: [InPrint(1)]);

        var upcoming = UpcomingGrouping.Group([overlord, slime, eightySix], Today);

        upcoming.OutNow.Select(entry => entry.SeriesTitle).ShouldBe(
            ["That Time I Got Reincarnated as a Slime", "86—EIGHTY-SIX", "Overlord"]);
    }

    [Fact]
    public void Group_TheOutNowLine_UsesTheFirstUnboughtVolumesArt()
    {
        var slime = CreateSeries("That Time I Got Reincarnated as a Slime", volumes: [InPrint(1, owned: true), InPrint(2), InPrint(3)]);
        slime.Volumes.Single(volume => volume.Number == 2).CoverObjectKey = "volume/slime-2.webp";

        UpcomingGrouping.Group([slime], Today).OutNow.ShouldHaveSingleItem()
            .CoverObjectKey.ShouldBe("volume/slime-2.webp");
    }

    [Fact]
    public void Group_AnEmptyShelf_IsEmptyGroupsRatherThanNothing()
    {
        var upcoming = UpcomingGrouping.Group([], Today);

        upcoming.OutNow.ShouldBeEmpty();
        upcoming.ThisMonth.ShouldBeEmpty();
        upcoming.NextThreeMonths.ShouldBeEmpty();
        upcoming.Later.ShouldBeEmpty();
        upcoming.Announced.ShouldBeEmpty();
    }
}
