using LnShelf.Client.Features.Upcoming;
using LnShelf.Shared.Releases;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The Upcoming timeline: six calendar months from this one, every dated volume in exactly one
/// of them or under Later — whichever of the server's three dated groups it came in.
/// </summary>
public sealed class ComingMonthsTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    [Fact]
    public void Build_Months_AreThisMonthAndTheFiveAfterIt()
    {
        var timeline = ComingMonths.Build(Upcoming(), Today);

        timeline.Months.Select(month => month.Month).ShouldBe(
        [
            new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1),
            new DateOnly(2027, 1, 1), new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 1),
        ]);
        timeline.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Build_ADatedVolume_LandsInItsCalendarMonthWhicheverGroupItCameIn()
    {
        // February is past the server's "next three months", so it arrives in Later — and is
        // still month five of the timeline.
        var upcoming = Upcoming(
            thisMonth: [Dated("86—EIGHTY-SIX", 15, new DateOnly(2026, 10, 31))],
            later: [Dated("The Saga of Tanya the Evil", 15, new DateOnly(2027, 2, 1))]);

        var timeline = ComingMonths.Build(upcoming, Today);

        timeline.Months[0].Entries.ShouldHaveSingleItem().SeriesTitle.ShouldBe("86—EIGHTY-SIX");
        timeline.Months[4].Entries.ShouldHaveSingleItem().SeriesTitle.ShouldBe("The Saga of Tanya the Evil");
        timeline.Later.ShouldBeEmpty();
        timeline.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Build_ADateAfterTheSixthMonth_IsLaterAndNotDropped()
    {
        var upcoming = Upcoming(later: [Dated("Torture Princess", 11, new DateOnly(2027, 4, 1))]);

        var timeline = ComingMonths.Build(upcoming, Today);

        timeline.Months.ShouldAllBe(month => month.Entries.Count == 0);
        timeline.Later.ShouldHaveSingleItem().VolumeNumbers.ShouldBe([11]);
    }

    [Fact]
    public void Build_TwoVolumesInOneMonth_AreEarliestFirst()
    {
        var upcoming = Upcoming(nextThreeMonths:
        [
            Dated("Overlord", 18, new DateOnly(2026, 12, 20)),
            Dated("Overlord", 17, new DateOnly(2026, 12, 2)),
        ]);

        var timeline = ComingMonths.Build(upcoming, Today);

        timeline.Months[2].Entries.Select(entry => entry.VolumeNumbers[0]).ShouldBe([17, 18]);
    }

    private static UpcomingResponse Upcoming(
        IReadOnlyList<UpcomingEntryResponse>? thisMonth = null,
        IReadOnlyList<UpcomingEntryResponse>? nextThreeMonths = null,
        IReadOnlyList<UpcomingEntryResponse>? later = null) => new()
        {
            OutNow = [],
            ThisMonth = thisMonth ?? [],
            NextThreeMonths = nextThreeMonths ?? [],
            Later = later ?? [],
            Announced = [],
        };

    private static UpcomingEntryResponse Dated(string title, int volumeNumber, DateOnly releaseDate) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        SeriesTitle = title,
        Format = Format.LightNovel,
        VolumeNumbers = [volumeNumber],
        VolumeIds = [Guid.CreateVersion7()],
        ReleaseDate = releaseDate,
    };
}
