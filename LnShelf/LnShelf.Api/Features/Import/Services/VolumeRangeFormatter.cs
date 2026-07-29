using System.Globalization;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Turns a set of volume numbers into something a person can check at a glance:
/// "1–9, 12, 15–16".
/// </summary>
// The review queue prints this so a mis-parsed volume number is visible *before*
// the batch is committed. A list of 13 numbers is not checkable; three ranges are.
public static class VolumeRangeFormatter
{
    /// <summary>En dash, not hyphen — it is a range, and design.md is typographic.</summary>
    private const char RangeSeparator = '–';

    public static string Format(IEnumerable<int?> volumeNumbers)
    {
        ArgumentNullException.ThrowIfNull(volumeNumbers);

        var numbers = volumeNumbers
            .Where(number => number is not null)
            .Select(number => number!.Value)
            .Distinct()
            .Order()
            .ToList();

        if (numbers.Count == 0)
        {
            return "no volume numbers";
        }

        var parts = new List<string>();
        var rangeStart = numbers[0];
        var rangeEnd = numbers[0];

        foreach (var number in numbers.Skip(1))
        {
            if (number == rangeEnd + 1)
            {
                rangeEnd = number;
                continue;
            }

            parts.Add(Describe(rangeStart, rangeEnd));
            rangeStart = number;
            rangeEnd = number;
        }

        parts.Add(Describe(rangeStart, rangeEnd));

        return string.Join(", ", parts);
    }

    private static string Describe(int start, int end) =>
        start == end ? start.ToString(CultureInfo.InvariantCulture)
        : end == start + 1 ? $"{start}, {end}"
        : $"{start}{RangeSeparator}{end}";
}
