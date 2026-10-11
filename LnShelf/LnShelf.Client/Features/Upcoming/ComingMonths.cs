using LnShelf.Shared.Releases;

namespace LnShelf.Client.Features.Upcoming;

/// <summary>One column of the "Coming" timeline: a calendar month and what is due in it.</summary>
/// <param name="Month">The first day of the month.</param>
/// <param name="Entries">Due in this month, earliest first. Empty for a quiet month.</param>
public sealed record ComingMonth(DateOnly Month, IReadOnlyList<UpcomingEntryResponse> Entries);

/// <summary>The timeline: six months from this one, and whatever is dated past them.</summary>
public sealed record ComingTimeline(IReadOnlyList<ComingMonth> Months, IReadOnlyList<UpcomingEntryResponse> Later)
{
    public bool IsEmpty => Later.Count == 0 && Months.All(month => month.Entries.Count == 0);
}

/// <summary>
/// Lays the dated groups out as the timeline the page draws: one column per month for the
/// current month and the five after it, then the rest.
/// </summary>
// Laying out only, never deciding: which volumes are dated and not out is the server's
// partition (UpcomingGrouping), and this concatenates its three dated groups back into one
// date-ordered list before cutting it by calendar month. The server's "next three months"
// boundary is a different cut from the timeline's six columns, and re-using it would leave
// months four to six empty.
public static class ComingMonths
{
    /// <summary>The current month and the five after it: half a year of release calendar.</summary>
    public const int Span = 6;

    public static ComingTimeline Build(UpcomingResponse upcoming, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(upcoming);

        var firstMonth = new DateOnly(today.Year, today.Month, 1);
        var afterWindow = firstMonth.AddMonths(Span);

        var dated = upcoming.ThisMonth
            .Concat(upcoming.NextThreeMonths)
            .Concat(upcoming.Later)
            .Where(entry => entry.ReleaseDate is not null)
            .OrderBy(entry => entry.ReleaseDate)
            .ToList();

        var months = Enumerable.Range(0, Span)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => new ComingMonth(
                month,
                [.. dated.Where(entry => entry.ReleaseDate >= month && entry.ReleaseDate < month.AddMonths(1))]))
            .ToList();

        return new ComingTimeline(months, [.. dated.Where(entry => entry.ReleaseDate >= afterWindow)]);
    }
}
