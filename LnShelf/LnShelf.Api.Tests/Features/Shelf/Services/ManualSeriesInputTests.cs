namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The number a volume added by hand takes, and the numbers it may not.
/// </summary>
public sealed class ManualSeriesInputTests
{
    public static TheoryData<decimal?, decimal[], decimal> Numbers => new()
    {
        // Nothing asked for: the next whole number after the highest one there.
        { null, [], 1m },
        { null, [1m, 2m, 3m], 4m },

        // A bonus volume at the top does not make the next one 8.5.
        { null, [1m, 7m, 7.5m], 8m },

        // Asked for: taken as it is.
        { 7.5m, [7m, 8m], 7.5m },
        { 12m, [1m], 12m },
    };

    [Theory]
    [MemberData(nameof(Numbers))]
    public void VolumeNumber_AValidRequest_IsTheNumberTheVolumeTakes(
        decimal? requested,
        decimal[] existing,
        decimal expected) =>
        ManualSeriesInput.VolumeNumber(requested, existing).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    [InlineData(7.125)]
    public void VolumeNumber_ANumberNoVolumeCanCarry_IsRefused(double requested) =>
        Should.Throw<InvalidInputException>(() => ManualSeriesInput.VolumeNumber((decimal)requested, []));

    [Fact]
    public void Title_LongerThanTheColumn_IsRefused() =>
        Should.Throw<InvalidInputException>(() =>
            ManualSeriesInput.Title(new string('x', AddManualSeriesRequest.MaximumTitleLength + 1)));

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" Carlo Zen ", "Carlo Zen")]
    public void Author_Blank_IsNoAuthor(string? author, string? expected) =>
        ManualSeriesInput.Author(author).ShouldBe(expected);
}
