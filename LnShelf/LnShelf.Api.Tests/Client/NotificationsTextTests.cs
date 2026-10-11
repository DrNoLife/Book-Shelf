using LnShelf.Client.Features.Notifications;
using LnShelf.Shared.Notifications;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// What the Notifications card says, above all on an iPhone: Web Push there needs the app on
/// the Home Screen, and the card has to say so rather than offer a button that cannot work.
/// </summary>
public sealed class NotificationsTextTests
{
    [Theory]
    // An iPhone in a Safari tab: no PushManager at all.
    [InlineData(true, false, false, "default", true)]
    // An iPhone in a tab that reports push anyway (an iPad on desktop mode): still the hint.
    [InlineData(true, true, false, "default", true)]
    // Installed to the Home Screen: nothing in the way.
    [InlineData(true, true, true, "default", false)]
    [InlineData(false, true, false, "default", false)]
    public void Obstacle_AppleDevices_NeedTheHomeScreen(
        bool isAppleMobile, bool supported, bool standalone, string permission, bool expectHint)
    {
        var status = new PushBrowserStatus
        {
            IsAppleMobile = isAppleMobile,
            Supported = supported,
            Standalone = standalone,
            Permission = permission,
        };

        var obstacle = NotificationsText.Obstacle(status);

        if (expectHint)
        {
            obstacle.ShouldBe(NotificationsText.HomeScreenHint);
        }
        else
        {
            obstacle.ShouldBeNull();
        }
    }

    [Fact]
    public void Obstacle_ABrowserWithoutPush_SaysSo() =>
        NotificationsText.Obstacle(new PushBrowserStatus { Supported = false })
            .ShouldNotBeNull().ShouldStartWith("This browser cannot receive notifications.");

    [Fact]
    public void Obstacle_PermissionDenied_PointsAtTheBrowserSettings() =>
        NotificationsText.Obstacle(new PushBrowserStatus { Supported = true, Permission = "denied" })
            .ShouldNotBeNull().ShouldContain("blocked");

    [Theory]
    [InlineData(0, "Chrome on Android · on since ")]
    [InlineData(1, "the last delivery failed")]
    [InlineData(3, "the last 3 deliveries failed")]
    public void DeviceLine_SaysWhatTheDeviceIsAndWhetherItIsFailing(int failures, string expected)
    {
        var device = new PushDeviceResponse
        {
            Id = Guid.CreateVersion7(),
            Endpoint = "https://fcm.googleapis.com/fcm/send/phone",
            DeviceLabel = "Chrome on Android",
            CreatedAt = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero),
            FailureCount = failures,
        };

        NotificationsText.DeviceLine(device).ShouldContain(expected);
    }

    [Theory]
    [InlineData(0, 0, "No device has notifications on.")]
    [InlineData(1, 1, "Sent. It should show up in a moment.")]
    [InlineData(3, 3, "Sent to all 3 devices.")]
    [InlineData(3, 2, "Sent to 2 of 3 devices.")]
    [InlineData(2, 0, "No device took it. Try again later.")]
    public void TestResult_CountsHonestly(int devices, int delivered, string expected) =>
        NotificationsText.TestResult(new PushTestResponse { Devices = devices, Delivered = delivered }).ShouldBe(expected);
}
