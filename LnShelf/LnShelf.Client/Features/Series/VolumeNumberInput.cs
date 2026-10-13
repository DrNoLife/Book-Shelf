namespace LnShelf.Client.Features.Series;

/// <summary>
/// What the "Add volume" number input accepts, in one place — as <see cref="ChapterInput"/> is
/// for the chapter input. The wording mirrors the API's own refusals, so a number caught here
/// reads the same as one the server would have turned down.
/// </summary>
// A text input with inputmode="decimal" rather than type="number", for the reason the chapter
// input has: a number input in a Danish browser is unreliable about "7.5" and "7,5". Text is
// read here, accepting either separator.
public static class VolumeNumberInput
{
    /// <returns>Null for an empty or unreadable value — nothing the reader meant as a number.</returns>
    public static decimal? Parse(string? text)
    {
        var trimmed = text?.Trim().Replace(',', '.');

        return decimal.TryParse(trimmed, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    /// <returns>The reason the number cannot be added, or null when it can.</returns>
    public static string? ProblemWith(decimal number, IEnumerable<decimal> existingNumbers)
    {
        ArgumentNullException.ThrowIfNull(existingNumbers);

        if (number <= 0 || number > AddLocalVolumeRequest.MaximumNumber)
        {
            return $"A volume number runs from above 0 to {AddLocalVolumeRequest.MaximumNumber.ToVolumeText()}.";
        }

        if (decimal.Round(number, 2) != number)
        {
            return "A volume number has at most two decimals, like 7.5.";
        }

        return existingNumbers.Contains(number)
            ? $"Vol {number.ToVolumeText()} is already there."
            : null;
    }
}
