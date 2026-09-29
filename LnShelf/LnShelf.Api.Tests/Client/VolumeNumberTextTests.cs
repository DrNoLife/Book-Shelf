using LnShelf.Client.Features.Upcoming;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// How the Upcoming view names the volumes a line is about: runs collapsed, so a series
/// twelve behind is one short phrase.
/// </summary>
public sealed class VolumeNumberTextTests
{
    [Theory]
    [InlineData(new[] { 15 }, "Vol 15")]
    [InlineData(new[] { 15, 16 }, "Vols 15–16")]
    [InlineData(new[] { 3, 5, 6, 7 }, "Vols 3, 5–7")]
    [InlineData(new[] { 7, 5, 6, 3 }, "Vols 3, 5–7")]
    [InlineData(new[] { 1, 3, 5 }, "Vols 1, 3, 5")]
    [InlineData(new[] { 4, 4 }, "Vol 4")]
    [InlineData(new int[0], "")]
    public void Describe_VolumeNumbers_CollapsesRuns(int[] numbers, string expected) =>
        VolumeNumberText.Describe(numbers).ShouldBe(expected);
}
