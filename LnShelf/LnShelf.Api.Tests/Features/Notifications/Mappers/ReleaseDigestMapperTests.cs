using LnShelf.Api.Features.Notifications.Mappers;

namespace LnShelf.Api.Tests.Features.Notifications.Mappers;

/// <summary>
/// One push per pass: a count in the title, the first three volumes in the body, and a click
/// that lands on the series when there is only one.
/// </summary>
public sealed class ReleaseDigestMapperTests
{
    private static readonly Series EightySix = new() { Id = Guid.CreateVersion7(), TitleEnglish = "86" };
    private static readonly Series Overlord = new() { Id = Guid.CreateVersion7(), TitleEnglish = "Overlord" };

    [Fact]
    public void ToNotification_TwoReleasesOnTwoSeries_CountsThemAndOpensTheShelf()
    {
        var notification = ReleaseDigestMapper.ToNotification(
        [
            Event(Overlord, ShelfEventKind.VolumeReleased, 17),
            Event(EightySix, ShelfEventKind.VolumeReleased, 14),
        ]);

        notification.Title.ShouldBe("2 new volumes out");
        notification.Body.ShouldBe("86 Vol 14, Overlord Vol 17");
        notification.Url.ShouldBe("/");
        notification.Tag.ShouldBe(ReleaseDigestMapper.Tag);
    }

    [Fact]
    public void ToNotification_OneSeries_OpensThatSeries()
    {
        var notification = ReleaseDigestMapper.ToNotification([Event(EightySix, ShelfEventKind.VolumeReleased, 14)]);

        notification.Title.ShouldBe("1 new volume out");
        notification.Url.ShouldBe($"/series/{EightySix.Id}");
    }

    [Fact]
    public void ToNotification_MoreThanThree_NamesThreeAndCountsTheRest()
    {
        var notification = ReleaseDigestMapper.ToNotification(
            [.. Enumerable.Range(1, 5).Select(number => Event(Overlord, ShelfEventKind.VolumeReleased, number))]);

        notification.Body.ShouldBe("Overlord Vol 1, Overlord Vol 2, Overlord Vol 3 +2 more");
    }

    public static TheoryData<ShelfEventKind, DateOnly?, DateOnly?, string> Descriptions => new()
    {
        // A first date is how an announcement arrives in practice (ShelfEventDiff).
        { ShelfEventKind.ReleaseDateChanged, null, new DateOnly(2026, 12, 12), "Overlord Vol 18 dated 12 Dec 2026" },
        { ShelfEventKind.ReleaseDateChanged, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 12), "Overlord Vol 18 moved to 12 Dec 2026" },
        { ShelfEventKind.VolumeAnnounced, null, new DateOnly(2027, 1, 5), "Overlord Vol 18 announced for 5 Jan 2027" },
        { ShelfEventKind.VolumeAnnounced, null, null, "Overlord Vol 18 announced" },
    };

    [Theory]
    [MemberData(nameof(Descriptions))]
    public void ToNotification_ADateOrAnnouncement_SaysWhatHappened(
        ShelfEventKind kind, DateOnly? previousDate, DateOnly? newDate, string expected)
    {
        var notification = ReleaseDigestMapper.ToNotification(
            [Event(Overlord, kind, 18, previousDate, newDate)]);

        notification.Title.ShouldBe("1 release update");
        notification.Body.ShouldBe(expected);
    }

    [Fact]
    public void ToNotification_ReleasesAndDates_PutsTheReleasesFirst()
    {
        var notification = ReleaseDigestMapper.ToNotification(
        [
            Event(Overlord, ShelfEventKind.ReleaseDateChanged, 18, newDate: new DateOnly(2026, 12, 12)),
            Event(EightySix, ShelfEventKind.VolumeReleased, 14),
        ]);

        notification.Title.ShouldBe("1 new volume out · 1 release update");
        notification.Body.ShouldBe("86 Vol 14, Overlord Vol 18 dated 12 Dec 2026");
    }

    [Fact]
    public void ToNotification_ABonusVolume_PrintsItsNumberWithAPoint()
    {
        var notification = ReleaseDigestMapper.ToNotification([Event(Overlord, ShelfEventKind.VolumeReleased, 7.5m)]);

        notification.Body.ShouldBe("Overlord Vol 7.5");
    }

    private static ShelfEvent Event(
        Series series,
        ShelfEventKind kind,
        decimal number,
        DateOnly? previousDate = null,
        DateOnly? newDate = null)
    {
        return new ShelfEvent
        {
            Id = Guid.CreateVersion7(),
            SeriesId = series.Id,
            Series = series,
            Kind = kind,
            VolumeNumber = number,
            PreviousDate = previousDate,
            NewDate = newDate,
            OccurredAt = DateTimeOffset.UnixEpoch,
        };
    }
}
