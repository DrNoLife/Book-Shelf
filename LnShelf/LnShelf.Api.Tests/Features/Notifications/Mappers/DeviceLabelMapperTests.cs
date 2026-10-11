using LnShelf.Api.Features.Notifications.Mappers;

namespace LnShelf.Api.Tests.Features.Notifications.Mappers;

/// <summary>A device's name in the profile's list, from the user agent it subscribed with.</summary>
public sealed class DeviceLabelMapperTests
{
    [Theory]
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36",
        "Chrome on Android")]
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
        "Safari on iPhone")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.0.0",
        "Edge on Windows")]
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.6; rv:131.0) Gecko/20100101 Firefox/131.0",
        "Firefox on Mac")]
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0.0.0 Mobile Safari/537.36",
        "Samsung Internet on Android")]
    [InlineData("curl/8.9.1", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FromUserAgent_NamesTheBrowserAndPlatform(string? userAgent, string? expected) =>
        DeviceLabelMapper.FromUserAgent(userAgent).ShouldBe(expected);
}
