namespace LnShelf.Api.Features.Calendar.Mappers;

/// <summary>
/// Writes the release calendar as an RFC 5545 iCalendar document: one all-day event per
/// dated volume.
/// </summary>
// Hand-written rather than an iCal library: the shelf needs one fixed shape of VEVENT, and
// the three rules that make a document valid (text escaping, 75-octet folding, CRLF) fit
// in this file. A library would be a dependency to keep patched for twenty lines.
public static class IcsCalendarWriter
{
    /// <summary>RFC 5545 §3.1: a content line is at most 75 octets, excluding its CRLF.</summary>
    public const int MaximumLineOctets = 75;

    private const string LineBreak = "\r\n";

    /// <param name="entries">The events, in the order they are written.</param>
    /// <param name="stamp">When the document was made; every event's DTSTAMP.</param>
    public static string Write(IEnumerable<CalendarEntry> entries, DateTimeOffset stamp)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var document = new StringBuilder();
        var dateStamp = stamp.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        AppendLine(document, "BEGIN:VCALENDAR");
        AppendLine(document, "VERSION:2.0");
        AppendLine(document, "PRODID:-//LnShelf//Release calendar//EN");
        AppendLine(document, "CALSCALE:GREGORIAN");
        AppendLine(document, "METHOD:PUBLISH");
        AppendLine(document, "X-WR-CALNAME:LN Shelf releases");
        // Hints, not rules: Apple and Outlook read these as how often to fetch again. Twelve
        // hours matches how often the dates can change (one nightly sync).
        AppendLine(document, "REFRESH-INTERVAL;VALUE=DURATION:PT12H");
        AppendLine(document, "X-PUBLISHED-TTL:PT12H");

        foreach (var entry in entries)
        {
            AppendLine(document, "BEGIN:VEVENT");
            AppendLine(document, $"UID:{entry.VolumeId}@lnshelf");
            AppendLine(document, $"DTSTAMP:{dateStamp}");
            AppendLine(document, $"DTSTART;VALUE=DATE:{FormatDate(entry.ReleaseDate)}");
            // All-day events end the next day: DTEND is exclusive.
            AppendLine(document, $"DTEND;VALUE=DATE:{FormatDate(entry.ReleaseDate.AddDays(1))}");
            AppendLine(document, $"SUMMARY:{EscapeText(Summary(entry))}");
            // A release is not a meeting: the day stays free in anyone's availability.
            AppendLine(document, "TRANSP:TRANSPARENT");
            AppendLine(document, "END:VEVENT");
        }

        AppendLine(document, "END:VCALENDAR");

        return document.ToString();
    }

    /// <summary>RFC 5545 §3.3.11: backslash, semicolon and comma escaped, newlines as \n.</summary>
    public static string EscapeText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// RFC 5545 §3.1: a line longer than 75 octets continues on the next line after a single
    /// space, with CRLF between them.
    /// </summary>
    // Counted in UTF-8 octets, not characters, and broken only between whole characters
    // (runes), so a Japanese title is never cut through the middle of a multi-byte
    // sequence. A continuation line's leading space is one of its 75 octets.
    public static string Fold(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var folded = new StringBuilder(line.Length + 8);
        var octetsOnLine = 0;

        foreach (var rune in line.EnumerateRunes())
        {
            if (octetsOnLine + rune.Utf8SequenceLength > MaximumLineOctets)
            {
                folded.Append(LineBreak).Append(' ');
                octetsOnLine = 1;
            }

            folded.Append(rune.ToString());
            octetsOnLine += rune.Utf8SequenceLength;
        }

        return folded.ToString();
    }

    /// <summary>"Series title Vol N", with the format named unless it is a light novel.</summary>
    // The light novel and the manga of one title are two series that usually share a title,
    // so without a label a calendar shows two identical "Overlord Vol 5" events. The light
    // novel is the shelf's default medium and stays unlabelled, as D7 wrote the summary.
    public static string Summary(CalendarEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var summary = $"{entry.SeriesTitle} Vol {entry.VolumeNumber.ToVolumeText()}";

        return entry.Format switch
        {
            Format.Manga => $"{summary} (Manga)",
            Format.WebNovel => $"{summary} (Web novel)",
            _ => summary,
        };
    }

    private static void AppendLine(StringBuilder document, string line) =>
        document.Append(Fold(line)).Append(LineBreak);

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
