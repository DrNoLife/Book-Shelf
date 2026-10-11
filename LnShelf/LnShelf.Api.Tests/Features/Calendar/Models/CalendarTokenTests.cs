using LnShelf.Api.Features.Calendar.Models;

namespace LnShelf.Api.Tests.Features.Calendar.Models;

/// <summary>
/// The feed's secret, and the shape check that keeps arbitrary input away from the database.
/// </summary>
public sealed class CalendarTokenTests
{
    [Fact]
    public void Create_Always_ReturnsUrlSafeCharactersThatFitTheColumn()
    {
        var token = CalendarToken.Create();

        token.Length.ShouldBe(CalendarToken.Length);
        token.Length.ShouldBeLessThanOrEqualTo(CalendarFeed.MaximumTokenLength);
        token.ShouldAllBe(character => char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_');
    }

    [Fact]
    public void Create_TwoCalls_ReturnDifferentTokens() =>
        CalendarToken.Create().ShouldNotBe(CalendarToken.Create());

    [Fact]
    public void IsWellFormed_ATokenFromCreate_IsTrue() =>
        CalendarToken.IsWellFormed(CalendarToken.Create()).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA/")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA.")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAA ")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAé")]
    public void IsWellFormed_MalformedInput_IsFalse(string? token) =>
        CalendarToken.IsWellFormed(token).ShouldBeFalse();
}
