using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The number the "Add volume" input reads, and the refusals it makes before the API would.
/// </summary>
public sealed class VolumeNumberInputTests
{
    [Theory]
    [InlineData("7", "7")]
    [InlineData("7,5", "7.5")]
    [InlineData(" 7.5 ", "7.5")]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("7.5.1", null)]
    public void Parse_TheTypedText_ReadsEitherSeparatorOrNothing(string text, string? expected) =>
        VolumeNumberInput.Parse(text).ShouldBe(expected is null ? null : decimal.Parse(expected, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("0", "A volume number runs from above 0 to 9999.")]
    [InlineData("-1", "A volume number runs from above 0 to 9999.")]
    [InlineData("10000", "A volume number runs from above 0 to 9999.")]
    [InlineData("7.125", "A volume number has at most two decimals, like 7.5.")]
    [InlineData("7", "Vol 7 is already there.")]
    [InlineData("7.5", null)]
    [InlineData("9999", null)]
    public void ProblemWith_AgainstSevenAndEight_NamesTheRefusal(string number, string? expected) =>
        VolumeNumberInput.ProblemWith(decimal.Parse(number, CultureInfo.InvariantCulture), [7m, 8m]).ShouldBe(expected);
}
