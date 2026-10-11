namespace LnShelf.Api.Tests.Contracts;

/// <summary>
/// How a volume number is written wherever a reader or a source sees it.
/// </summary>
public sealed class VolumeNumberExtensionsTests
{
    [Theory]
    // Text in, because an attribute cannot carry a decimal. "7.0" is what a scaled column
    // would hand back for a main volume, and it must still print as 7.
    [InlineData("7", "7")]
    [InlineData("7.0", "7")]
    [InlineData("7.5", "7.5")]
    [InlineData("11.5", "11.5")]
    public void ToVolumeText_AVolumeNumber_IsWrittenWithoutTrailingZeros(string number, string expected) =>
        decimal.Parse(number, CultureInfo.InvariantCulture).ToVolumeText().ShouldBe(expected);

    [Fact]
    public void ToVolumeText_InADanishBrowser_StillUsesAPoint()
    {
        // The client runs in the reader's culture; plain interpolation there prints "7,5".
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");

        try
        {
            7.5m.ToVolumeText().ShouldBe("7.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("7", false)]
    [InlineData("7.0", false)]
    [InlineData("7.5", true)]
    public void IsBonusVolume_AVolumeNumber_IsTrueOnlyWithAFraction(string number, bool expected) =>
        decimal.Parse(number, CultureInfo.InvariantCulture).IsBonusVolume.ShouldBe(expected);
}
