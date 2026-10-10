namespace LnShelf.Client.Features.Series;

/// <summary>
/// What a native date input says and accepts, in one place: its wire format, the range
/// the shelf allows, and the sentence for a date outside it.
/// </summary>
// The input already enforces min and max when the date is picked, but a typed date can
// still be out of range, and the API's refusal arrives as a bare status code. Checking here
// first means the reader is told why, in words.
public static class ReadDateInput
{
    private const string WireFormat = "yyyy-MM-dd";

    /// <summary>The browser's calendar day, which is the day the reader means by "today".</summary>
    public static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime);

    public static string EarliestText => Format(ReadDateLimits.Earliest);

    /// <summary>The value an <c>input type="date"</c> takes: ISO, or empty for no date.</summary>
    public static string Format(DateOnly? date) =>
        date?.ToString(WireFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <returns>Null for an empty or unreadable value — the input's way of saying "no date".</returns>
    public static DateOnly? Parse(string? text) =>
        DateOnly.TryParseExact(text, WireFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <returns>The reason the date cannot be saved, or null when it can.</returns>
    public static string? ProblemWith(DateOnly date) =>
        date > Today ? "A volume cannot have been read in the future."
        : date < ReadDateLimits.Earliest ? $"A read date cannot be before {ReadDateLimits.Earliest.Full}."
        : null;
}
