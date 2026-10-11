namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// The checks a series or volume added by hand passes before anything is written, each a 400
/// in words the form can print.
/// </summary>
// Apart from the command service so the service reads as the write it is, and so the rules
// are one static, testable thing rather than guard clauses spread across three methods.
public static class ManualSeriesInput
{
    /// <returns>The title, trimmed.</returns>
    public static string Title(string? title)
    {
        var trimmed = title.NullIfBlank()
            ?? throw new InvalidInputException("A series needs a title.");

        if (trimmed.Length > AddManualSeriesRequest.MaximumTitleLength)
        {
            throw new InvalidInputException(
                $"A title can be at most {AddManualSeriesRequest.MaximumTitleLength} characters; this one is {trimmed.Length}.");
        }

        return trimmed;
    }

    /// <returns>The author, trimmed, or null when none was given.</returns>
    public static string? Author(string? author)
    {
        var trimmed = author.NullIfBlank();

        if (trimmed is { Length: > AddManualSeriesRequest.MaximumAuthorLength })
        {
            throw new InvalidInputException(
                $"An author can be at most {AddManualSeriesRequest.MaximumAuthorLength} characters; this one is {trimmed.Length}.");
        }

        return trimmed;
    }

    public static int VolumeCount(int? volumeCount)
    {
        var count = volumeCount ?? 0;

        if (count is < 0 or > AddManualSeriesRequest.MaximumVolumeCount)
        {
            throw new InvalidInputException(
                $"A series added by hand starts with 0 to {AddManualSeriesRequest.MaximumVolumeCount} volumes.");
        }

        return count;
    }

    /// <summary>
    /// The number a new volume takes: the one asked for, or the next whole number after the
    /// highest one the series has.
    /// </summary>
    // Two decimal places at most, because that is what ToVolumeText prints: a 7.125 would be
    // shown as "7.13" and collide on screen with a volume that really is 7.13.
    public static decimal VolumeNumber(decimal? requested, IEnumerable<decimal> existingNumbers)
    {
        var number = requested ?? decimal.Truncate(existingNumbers.DefaultIfEmpty(0m).Max()) + 1;

        if (number <= 0 || number > AddLocalVolumeRequest.MaximumNumber)
        {
            throw new InvalidInputException(
                $"A volume number runs from above 0 to {AddLocalVolumeRequest.MaximumNumber.ToVolumeText()}.");
        }

        if (decimal.Round(number, 2) != number)
        {
            throw new InvalidInputException("A volume number has at most two decimals, like 7.5.");
        }

        return number;
    }
}
