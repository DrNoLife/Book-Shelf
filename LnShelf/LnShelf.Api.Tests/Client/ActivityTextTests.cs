using LnShelf.Client.Features.Activity;
using LnShelf.Shared.Activity;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// "Since your last visit" in words: a first date is not a move, and releases share a line.
/// </summary>
public sealed class ActivityTextTests
{
    private static readonly DateOnly TwelfthOfDecember = new(2026, 12, 12);

    public static TheoryData<ShelfEventKind, DateOnly?, DateOnly?, string> Events => new()
    {
        { ShelfEventKind.VolumeReleased, null, null, "Vol 18 is out" },
        { ShelfEventKind.VolumeAnnounced, null, null, "Vol 18 announced" },
        { ShelfEventKind.VolumeAnnounced, null, TwelfthOfDecember, "Vol 18 announced for 12 Dec 2026" },

        // How a newly announced volume shows up in practice: dated for the first time.
        { ShelfEventKind.ReleaseDateChanged, null, TwelfthOfDecember, "Vol 18 dated 12 Dec 2026" },
        { ShelfEventKind.ReleaseDateChanged, new DateOnly(2026, 11, 20), TwelfthOfDecember, "Vol 18 moved to 12 Dec 2026" },
    };

    [Theory]
    [MemberData(nameof(Events))]
    public void For_AnEvent_SaysWhatHappened(ShelfEventKind kind, DateOnly? previousDate, DateOnly? newDate, string expected)
    {
        ActivityText.For(Event(kind, 18, previousDate, newDate)).ShouldBe(expected);
    }

    [Fact]
    public void Lines_SeveralReleasesAndADate_PutsTheReleasesOnOneLineFirst()
    {
        var series = new ActivitySeriesResponse
        {
            SeriesId = Guid.NewGuid(),
            SeriesTitle = "Overlord",
            Format = Format.LightNovel,
            Events =
            [
                Event(ShelfEventKind.ReleaseDateChanged, 16, null, TwelfthOfDecember),
                Event(ShelfEventKind.VolumeReleased, 15, null, null),
                Event(ShelfEventKind.VolumeReleased, 14, null, null),
            ],
        };

        ActivityText.Lines(series).ShouldBe(["Vols 14–15 are out", "Vol 16 dated 12 Dec 2026"]);
    }

    private static ActivityEventResponse Event(ShelfEventKind kind, decimal volumeNumber, DateOnly? previousDate, DateOnly? newDate) => new()
    {
        Kind = kind,
        VolumeNumber = volumeNumber,
        PreviousDate = previousDate,
        NewDate = newDate,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };
}
