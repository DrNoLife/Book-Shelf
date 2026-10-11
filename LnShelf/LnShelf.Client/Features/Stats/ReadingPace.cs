using LnShelf.Shared.Stats;

namespace LnShelf.Client.Features.Stats;

/// <summary>
/// "8 a month" and what it means for the backlog: the Stats page's Pace card.
/// </summary>
/// <param name="PerMonth">Volumes read per month over <see cref="FirstMonth"/>–<see cref="LastMonth"/>.</param>
/// <param name="FirstMonth">The first month averaged over.</param>
/// <param name="LastMonth">The last month averaged over: the last whole one.</param>
public sealed record ReadingPace(double PerMonth, DateOnly FirstMonth, DateOnly LastMonth)
{
    /// <summary>How many whole months the pace is averaged over.</summary>
    public const int MonthsAveraged = 3;

    // 365.25 / 12 / 7: a month of reading in weeks, for "about six weeks of reading".
    private const double WeeksPerMonth = 4.35;

    /// <summary>
    /// The average over the last three whole months, or null when none of them has a dated
    /// read — a pace of zero would say "never" about a backlog that is merely undated.
    /// </summary>
    // Whole months only: the current one is still filling, and averaging it in would drag the
    // pace down every first week of a month.
    public static ReadingPace? From(IReadOnlyList<MonthlyReadResponse> readPerMonth)
    {
        ArgumentNullException.ThrowIfNull(readPerMonth);

        // The last entry is the current month (StatsResponse.ReadPerMonth).
        var whole = readPerMonth.SkipLast(1).TakeLast(MonthsAveraged).ToList();
        if (whole.Count < MonthsAveraged || whole.Sum(month => month.Total) == 0)
        {
            return null;
        }

        return new ReadingPace(whole.Average(month => month.Total), whole[0].Month, whole[^1].Month);
    }

    /// <summary>"8" — whole volumes, except under one a month, where "0.3" beats a flat "0".</summary>
    public string PerMonthText => PerMonth < 1
        ? PerMonth.ToString("0.#", CultureInfo.InvariantCulture)
        : Math.Round(PerMonth, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    /// <summary>"Aug–Oct" — the months the pace is averaged over.</summary>
    public string MonthsText =>
        $"{FirstMonth.ToString("MMM", CultureInfo.InvariantCulture)}–{LastMonth.ToString("MMM", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// "about six weeks of reading at this pace" for <paramref name="waiting"/> owned, unread
    /// volumes. Weeks up to two months' worth, months after that — nobody plans in 30 weeks.
    /// </summary>
    public string Describe(int waiting)
    {
        var months = (double)waiting / PerMonth;
        var weeks = (int)Math.Max(1, Math.Round(months * WeeksPerMonth, MidpointRounding.AwayFromZero));

        if (weeks <= 8)
        {
            return weeks == 1 ? "about a week of reading at this pace" : $"about {weeks} weeks of reading at this pace";
        }

        var wholeMonths = (int)Math.Round(months, MidpointRounding.AwayFromZero);
        return wholeMonths > 12
            ? "more than a year of reading at this pace"
            : $"about {wholeMonths} months of reading at this pace";
    }
}
