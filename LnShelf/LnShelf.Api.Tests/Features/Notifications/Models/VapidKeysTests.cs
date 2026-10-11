using System.Buffers.Text;
using Lib.Net.Http.WebPush.Authentication;
using LnShelf.Api.Features.Notifications.Models;

namespace LnShelf.Api.Tests.Features.Notifications.Models;

/// <summary>What <c>vapid-keys</c> prints is a pair the push library and the validator both accept.</summary>
public sealed class VapidKeysTests
{
    [Fact]
    public void Create_APair_IsAcceptedByTheLibraryThatSignsWithIt()
    {
        var keys = VapidKeys.Create();

        VapidKeys.IsPublicKey(keys.PublicKey).ShouldBeTrue();
        VapidKeys.DecodesTo(keys.PrivateKey, VapidKeys.PrivateKeyLength).ShouldBeTrue();

        // The library checks the lengths and the curve itself, and throws on a bad pair.
        using var authentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey);
        authentication.PublicKey.ShouldBe(keys.PublicKey);
    }

    [Fact]
    public void Create_TwoCalls_GiveTwoPairs() =>
        VapidKeys.Create().PrivateKey.ShouldNotBe(VapidKeys.Create().PrivateKey);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64url!")]
    [InlineData("AAAA")]
    public void IsPublicKey_AnythingButAPoint_IsFalse(string? value) =>
        VapidKeys.IsPublicKey(value).ShouldBeFalse();

    [Fact]
    public void IsPublicKey_SixtyFiveBytesOffTheCurve_IsFalse() =>
        VapidKeys.IsPublicKey(Base64Url.EncodeToString([0x04, .. Enumerable.Repeat((byte)1, 64)])).ShouldBeFalse();
}
