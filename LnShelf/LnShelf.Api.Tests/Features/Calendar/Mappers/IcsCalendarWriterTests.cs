using LnShelf.Api.Features.Calendar.Mappers;
using LnShelf.Api.Features.Calendar.Models;

namespace LnShelf.Api.Tests.Features.Calendar.Mappers;

/// <summary>
/// The three rules a calendar app rejects a feed over: escaping, 75-octet folding, CRLF.
/// </summary>
public sealed class IcsCalendarWriterTests
{
    private static readonly Guid VolumeId = new("0199a0f1-1111-7000-8000-000000000001");

    // 22:30 in Copenhagen summer time is 20:30 UTC: DTSTAMP must be the UTC instant.
    private static readonly DateTimeOffset Stamp = new(2026, 10, 11, 22, 30, 5, TimeSpan.FromHours(2));

    [Theory]
    [InlineData("Overlord", "Overlord")]
    [InlineData("Re:Zero, Starting Life", "Re:Zero\\, Starting Life")]
    [InlineData("A; B", "A\\; B")]
    [InlineData("C:\\path", "C:\\\\path")]
    [InlineData("One\nTwo", "One\\nTwo")]
    [InlineData("One\r\nTwo", "One\\nTwo")]
    public void EscapeText_SpecialCharacters_AreEscapedPerRfc5545(string value, string expected) =>
        IcsCalendarWriter.EscapeText(value).ShouldBe(expected);

    [Fact]
    public void Fold_ShortLine_IsUnchanged() =>
        IcsCalendarWriter.Fold("SUMMARY:Overlord Vol 17").ShouldBe("SUMMARY:Overlord Vol 17");

    [Theory]
    [InlineData("SUMMARY:", "A", 200)]
    // Three octets a character: a naive character count would put 75 of these on one line.
    [InlineData("SUMMARY:", "本", 120)]
    // Four octets and a surrogate pair in UTF-16: must never be split between its halves.
    [InlineData("SUMMARY:", "😀", 60)]
    public void Fold_LongLine_KeepsEveryLineWithin75OctetsAndLosesNothing(string prefix, string character, int count)
    {
        var line = prefix + string.Concat(Enumerable.Repeat(character, count));

        var folded = IcsCalendarWriter.Fold(line);

        var physicalLines = folded.Split("\r\n");
        physicalLines.Length.ShouldBeGreaterThan(1);
        physicalLines.ShouldAllBe(physical => Encoding.UTF8.GetByteCount(physical) <= IcsCalendarWriter.MaximumLineOctets);
        physicalLines.Skip(1).ShouldAllBe(physical => physical.StartsWith(' '));
        // Unfolding (RFC 5545 §3.1: drop each CRLF and the one space after it) gives the line back.
        folded.Replace("\r\n ", string.Empty, StringComparison.Ordinal).ShouldBe(line);
        // Every piece is valid text on its own: a surrogate half left alone by a cut inside a
        // character would enumerate as the replacement character.
        physicalLines.ShouldAllBe(physical => physical.EnumerateRunes().All(rune => rune != Rune.ReplacementChar));
    }

    [Fact]
    public void Write_OneVolume_WritesAnAllDayEventWithAStableUid()
    {
        var document = IcsCalendarWriter.Write(
            [new CalendarEntry(VolumeId, "The Saga of Tanya the Evil", Format.LightNovel, 7.5m, new DateOnly(2026, 12, 31))],
            Stamp);

        var lines = document.Split("\r\n");
        lines.ShouldContain("BEGIN:VCALENDAR");
        lines.ShouldContain("VERSION:2.0");
        lines.ShouldContain($"UID:{VolumeId}@lnshelf");
        lines.ShouldContain("DTSTAMP:20261011T203005Z");
        lines.ShouldContain("DTSTART;VALUE=DATE:20261231");
        // Exclusive end, across a year boundary.
        lines.ShouldContain("DTEND;VALUE=DATE:20270101");
        lines.ShouldContain("SUMMARY:The Saga of Tanya the Evil Vol 7.5");
        lines.Count(line => line == "BEGIN:VEVENT").ShouldBe(1);
        document.ShouldEndWith("END:VCALENDAR\r\n");
    }

    [Fact]
    public void Write_Always_UsesCrlfAndNoBareLineFeeds()
    {
        var document = IcsCalendarWriter.Write(
            [new CalendarEntry(VolumeId, "Title\nwith a line break", Format.LightNovel, 1m, new DateOnly(2026, 10, 1))],
            Stamp);

        document.Replace("\r\n", string.Empty, StringComparison.Ordinal).ShouldNotContain('\n');
        document.Replace("\r\n", string.Empty, StringComparison.Ordinal).ShouldNotContain('\r');
    }

    [Theory]
    [InlineData(Format.LightNovel, "SUMMARY:Overlord Vol 5")]
    [InlineData(Format.Manga, "SUMMARY:Overlord Vol 5 (Manga)")]
    [InlineData(Format.WebNovel, "SUMMARY:Overlord Vol 5 (Web novel)")]
    public void Write_SameTitleInTwoFormats_TheSummaryTellsThemApart(Format format, string expected)
    {
        // The light novel and the manga of one title are two series with one title: without
        // the label a calendar shows two identical events, maybe on the same day.
        var document = IcsCalendarWriter.Write(
            [new CalendarEntry(VolumeId, "Overlord", format, 5m, new DateOnly(2026, 12, 1))],
            Stamp);

        document.Split("\r\n").ShouldContain(expected);
    }

    [Fact]
    public void Write_NoEntries_IsStillAValidEmptyCalendar()
    {
        var document = IcsCalendarWriter.Write([], Stamp);

        document.ShouldStartWith("BEGIN:VCALENDAR\r\n");
        document.ShouldEndWith("END:VCALENDAR\r\n");
        document.ShouldNotContain("BEGIN:VEVENT");
    }
}
