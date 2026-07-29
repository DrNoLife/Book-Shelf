namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The line the review queue prints so a mis-parsed volume number is visible
/// before anything is committed.
/// </summary>
public sealed class VolumeRangeFormatterTests
{
    [Theory]
    // The real groups from the export.
    [InlineData(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 }, "1–13")]
    [InlineData(new[] { 2, 3, 4, 5, 6, 7, 8, 9, 15 }, "2–9, 15")]
    [InlineData(new[] { 1, 13, 15, 16 }, "1, 13, 15, 16")]
    [InlineData(new[] { 1, 10, 11, 12, 13, 14 }, "1, 10–14")]
    [InlineData(new[] { 1, 2, 3 }, "1–3")]
    [InlineData(new[] { 5 }, "5")]
    // Two adjacent numbers read better as a pair than as a range.
    [InlineData(new[] { 7, 8 }, "7, 8")]
    // Order and duplicates come from the file, not from the reader's assumptions.
    [InlineData(new[] { 3, 1, 2, 2 }, "1–3")]
    public void Format_VolumeNumbers_CompressesToRanges(int[] volumeNumbers, string expected) =>
        VolumeRangeFormatter.Format(volumeNumbers.Select(number => (int?)number)).ShouldBe(expected);

    [Fact]
    public void Format_NoNumbersAtAll_SaysSo() =>
        VolumeRangeFormatter.Format([null, null]).ShouldBe("no volume numbers");

    [Fact]
    public void Format_Empty_SaysSo() =>
        VolumeRangeFormatter.Format([]).ShouldBe("no volume numbers");
}
