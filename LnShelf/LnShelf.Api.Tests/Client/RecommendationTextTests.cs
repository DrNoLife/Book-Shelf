using LnShelf.Client.Features.Recommendations;

namespace LnShelf.Api.Tests.Client;

/// <summary>The recommendation strip and For you in words: singular votes, quiet extras.</summary>
public sealed class RecommendationTextTests
{
    [Theory]
    [InlineData(1, "1 vote")]
    [InlineData(2, "2 votes")]
    [InlineData(124, "124 votes")]
    public void Votes_PluralisesOnlyOne(int count, string expected) =>
        Assert.Equal(expected, RecommendationText.Votes(count));

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "+ 1 more of yours")]
    [InlineData(3, "+ 3 more of yours")]
    public void OtherSeeds_SaysNothingWhenThereAreNone(int count, string? expected) =>
        Assert.Equal(expected, RecommendationText.OtherSeeds(count));

    [Theory]
    [InlineData(1, "From 1 series you've read")]
    [InlineData(12, "From 12 series you've read")]
    public void SeedLine_PluralisesOnlyOne(int count, string expected) =>
        Assert.Equal(expected, RecommendationText.SeedLine(count));

    [Fact]
    public void HeadingsAndLabels_NameTheTitle()
    {
        Assert.Equal("Readers of Spice and Wolf also liked", RecommendationText.AlsoLikedHeading("Spice and Wolf"));
        Assert.Equal("Because you read Spice and Wolf", RecommendationText.BecauseOfHeading("Spice and Wolf"));
        Assert.Equal("Spice and Wolf won't be suggested again.", RecommendationText.Hidden("Spice and Wolf"));
        Assert.Equal("Not interested (4)", RecommendationText.DismissedSummary(4));
        Assert.Equal("Not interested in Spice and Wolf", RecommendationText.NotInterestedLabel("Spice and Wolf"));
        Assert.Equal("Show Spice and Wolf again", RecommendationText.ShowAgainLabel("Spice and Wolf"));
    }
}
