using LnShelf.Client.Features.Stats;
using LnShelf.Shared.Stats;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The Stats page's pace: averaged over the last three whole months, never the one still
/// filling, and put into words for the volumes bought and waiting.
/// </summary>
public sealed class ReadingPaceTests
{
    [Fact]
    public void From_TheLastThreeWholeMonths_AreAveragedAndTheCurrentOneIsLeftOut()
    {
        // Jul 6, Aug 9, Sep 9 — and October, still filling, at 2.
        var pace = ReadingPace.From(Months(0, 0, 0, 0, 0, 0, 0, 0, 6, 9, 9, 2)).ShouldNotBeNull();

        pace.PerMonth.ShouldBe(8);
        pace.PerMonthText.ShouldBe("8");
        pace.MonthsText.ShouldBe("Jul–Sep");
    }

    [Fact]
    public void From_NoDatedReadInThoseMonths_IsNoPace() =>
        ReadingPace.From(Months(5, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4)).ShouldBeNull();

    [Fact]
    public void PerMonthText_UnderOneAMonth_KeepsADecimalRatherThanSayingZero()
    {
        var pace = ReadingPace.From(Months(0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0)).ShouldNotBeNull();

        pace.PerMonthText.ShouldBe("0.3");
    }

    [Theory]
    [InlineData(8, 12, "about 7 weeks of reading at this pace")]
    [InlineData(8, 2, "about a week of reading at this pace")]
    [InlineData(8, 40, "about 5 months of reading at this pace")]
    [InlineData(1, 30, "more than a year of reading at this pace")]
    public void Describe_TheWaitingVolumes_InWeeksThenMonths(int perMonth, int waiting, string expected)
    {
        var pace = new ReadingPace(perMonth, new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 1));

        pace.Describe(waiting).ShouldBe(expected);
    }

    private static List<MonthlyReadResponse> Months(params int[] totals) =>
    [
        .. totals.Select((total, index) => new MonthlyReadResponse
        {
            Month = new DateOnly(2025, 11, 1).AddMonths(index),
            LightNovel = total,
            Manga = 0,
            WebNovel = 0,
        }),
    ];
}
