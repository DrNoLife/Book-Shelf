namespace LnShelf.Client.Features.Upcoming;

/// <summary>
/// "Vol 15", "Vols 15–16", "Vols 3, 5–7" — a list of volume numbers as the Upcoming view
/// prints it.
/// </summary>
// Collapsed into runs because a series twelve volumes behind is one line, and a line that
// spelled out "1, 2, 3, 4, …, 12" would be longer than the title it sits beside.
public static class VolumeNumberText
{
    public static string Describe(IReadOnlyList<int> numbers)
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
            if (number == previous + 1)
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

    private static string Run(int start, int end) => start == end
        ? start.ToString(CultureInfo.InvariantCulture)
        : $"{start.ToString(CultureInfo.InvariantCulture)}–{end.ToString(CultureInfo.InvariantCulture)}";
}
