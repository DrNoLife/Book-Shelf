namespace LnShelf.Client.Features.Upcoming;

/// <summary>
/// "Vol 15", "Vols 15–16", "Vols 3, 5–7", "Vols 7, 7.5, 8–9" — a list of volume numbers as
/// the Upcoming view prints it.
/// </summary>
// Collapsed into runs because a series twelve volumes behind is one line, and a line that
// spelled out "1, 2, 3, 4, …, 12" would be longer than the title it sits beside.
//
// A bonus volume never joins a run. "Vols 7–8" would hide that 7.5 is in there, and
// "Vols 7–7.5" reads as a typo; standing alone it says exactly which books are meant.
public static class VolumeNumberText
{
    public static string Describe(IReadOnlyList<decimal> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);

        if (numbers.Count == 0)
        {
            return string.Empty;
        }

        var sorted = numbers.Distinct().Order().ToList();
        var runs = new List<string>();
        var start = sorted[0];
        var previous = sorted[0];

        foreach (var number in sorted.Skip(1))
        {
            if (!number.IsBonusVolume && !previous.IsBonusVolume && number == previous + 1)
            {
                previous = number;
                continue;
            }

            runs.Add(Run(start, previous));
            start = previous = number;
        }

        runs.Add(Run(start, previous));

        var prefix = sorted.Count == 1 ? "Vol" : "Vols";
        return $"{prefix} {string.Join(", ", runs)}";
    }

    private static string Run(decimal start, decimal end) => start == end
        ? start.ToVolumeText()
        : $"{start.ToVolumeText()}–{end.ToVolumeText()}";
}
