using LnShelf.Api.Extensions;

namespace LnShelf.Api.Tests.Extensions;

/// <summary>
/// <c>TimeProvider.Today()</c> answers the local calendar day, not the UTC one.
/// </summary>
// The whole point of the extension is that the six services that used to write the
// expression out longhand cannot disagree about which clock "today" is on, so the test that
// earns its place is the one at the boundary where the two clocks give different answers.
public sealed class TimeProviderExtensionsTests
{
    [Theory]
    // Late evening in UTC, already tomorrow in a +02:00 zone: a volume released "today"
    // there must not read as unreleased.
    [InlineData("2026-08-15T23:30:00Z", 2, "2026-08-16")]
    // The mirror case — early morning in UTC, still yesterday in a -05:00 zone.
    [InlineData("2026-08-16T01:30:00Z", -5, "2026-08-15")]
    // And the uninteresting middle of the day, where both clocks agree.
    [InlineData("2026-08-16T12:00:00Z", 0, "2026-08-16")]
    public void Today_AtAUtcDayBoundary_IsTheLocalCalendarDay(
        string utcNow, int localOffsetHours, string expected)
    {
        var timeProvider = new FixedTimeProvider(
            DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            TimeSpan.FromHours(localOffsetHours));

        timeProvider.Today().ShouldBe(DateOnly.Parse(expected, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A clock stopped at a chosen instant, in a zone with a chosen fixed offset.
    /// </summary>
    // A custom zone rather than a named one (Europe/Copenhagen): a fixed offset has no
    // daylight-saving rules to argue with and needs no time-zone database on the machine
    // running the test.
    private sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeSpan localOffset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("LnShelf test zone", localOffset, "LnShelf test zone", "LnShelf test zone");
    }
}
