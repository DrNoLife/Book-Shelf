using LnShelf.Api.Features.Notifications.Models;
using LnShelf.Api.Features.Notifications.Services;

namespace LnShelf.Api.Tests.Features.Notifications.Services;

/// <summary>Web Push is all three settings or none; half set up refuses to boot, naming the field.</summary>
public sealed class WebPushOptionsValidatorTests
{
    private static readonly VapidKeys Keys = VapidKeys.Create();

    [Fact]
    public void Validate_NothingSet_IsTheFeatureSwitchedOff() =>
        Validate(new WebPushOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_AllThreeSet_Succeeds() =>
        Validate(Options(Keys.PublicKey, Keys.PrivateKey, "mailto:owner@example.com")).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("", "private", "mailto:owner@example.com", "PublicKey")]
    [InlineData("public", "", "mailto:owner@example.com", "PrivateKey")]
    [InlineData("public", "private", "", "Subject")]
    [InlineData("public", "private", "owner@example.com", "Subject")]
    [InlineData("not-a-key", "private", "mailto:owner@example.com", "PublicKey")]
    [InlineData("public", "not-a-key", "https://lnshelf.example.com", "PrivateKey")]
    public void Validate_HalfSetUp_FailsNamingTheField(string publicKey, string privateKey, string subject, string field)
    {
        var options = Options(
            publicKey == "public" ? Keys.PublicKey : publicKey,
            privateKey == "private" ? Keys.PrivateKey : privateKey,
            subject);

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain($"WebPush:{field}");
    }

    private static ValidateOptionsResult Validate(WebPushOptions options) =>
        new WebPushOptionsValidator().Validate(name: null, options);

    private static WebPushOptions Options(string publicKey, string privateKey, string subject) => new()
    {
        PublicKey = publicKey,
        PrivateKey = privateKey,
        Subject = subject,
    };
}
