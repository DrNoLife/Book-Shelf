namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// When the nightly sync next fires: 04:00 on the local wall clock, including across the two
/// nights a year the wall clock moves.
/// </summary>
// Against a real zone rather than a fixed offset, because a fixed offset is exactly the
// assumption that was wrong. Copenhagen is the shelf's own: 2026's changes are 29 March
// (02:00 CET → 03:00 CEST) and 25 October (03:00 CEST → 02:00 CET).
public sealed class NightlySyncBackgroundServiceTests
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static TheoryData<DateTimeOffset, DateTimeOffset> NextRuns => new()
    {
        // An ordinary summer evening: 22:00 CEST, next run 04:00 CEST.
        { Utc(2026, 7, 10, 20, 0), Utc(2026, 7, 11, 2, 0) },

        // The evening before the clocks go back: 22:00 CEST, and the run is 04:00 *CET* — seven
        // hours away, not six. Reusing tonight's offset fired it at 03:00 local.
        { Utc(2026, 10, 24, 20, 0), Utc(2026, 10, 25, 3, 0) },

        // The evening before the clocks go forward: 22:00 CET, and the run is 04:00 *CEST* —
        // five hours away, not six. Reusing tonight's offset fired it at 05:00 local.
        { Utc(2026, 3, 28, 21, 0), Utc(2026, 3, 29, 2, 0) },

        // Past today's run already: 05:00 CET, so tomorrow's.
        { Utc(2026, 1, 15, 4, 0), Utc(2026, 1, 16, 3, 0) },

        // Just before today's run: 03:30 CEST, so the one in half an hour.
        { Utc(2026, 7, 10, 1, 30), Utc(2026, 7, 10, 2, 0) },
    };

    [Theory]
    [MemberData(nameof(NextRuns))]
    public void TimeUntilNextRun_AnyNight_LandsOnFourOClockLocal(DateTimeOffset utcNow, DateTimeOffset expectedRun)
    {
        var delay = NightlySyncBackgroundService.TimeUntilNextRun(utcNow, Copenhagen, hour: 4);

        (utcNow + delay).ShouldBe(expectedRun);
        TimeZoneInfo.ConvertTime(utcNow + delay, Copenhagen).Hour.ShouldBe(4);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
