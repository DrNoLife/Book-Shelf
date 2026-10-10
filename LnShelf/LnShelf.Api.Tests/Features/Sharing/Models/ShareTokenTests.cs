using LnShelf.Api.Features.Sharing.Models;

namespace LnShelf.Api.Tests.Features.Sharing.Models;

/// <summary>
/// The link's secret, and the shape check that keeps arbitrary input away from the database.
/// </summary>
public sealed class ShareTokenTests
{
    [Fact]
    public void Create_Always_Returns22UrlSafeCharacters()
    {
        var token = ShareToken.Create();

        token.Length.ShouldBe(ShareToken.Length);
        token.ShouldAllBe(character => char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_');
    }

    [Fact]
    public void Create_TwoCalls_ReturnDifferentTokens() =>
        ShareToken.Create().ShouldNotBe(ShareToken.Create());

    [Fact]
    public void IsWellFormed_ATokenFromCreate_IsTrue() =>
        ShareToken.IsWellFormed(ShareToken.Create()).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA.")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA%")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAé")]
    public void IsWellFormed_MalformedInput_IsFalse(string? token) =>
        ShareToken.IsWellFormed(token).ShouldBeFalse();
}
