using System.Buffers.Text;
using System.Net;
using LnShelf.Api.Features.Releases.Models;
using LnShelf.Api.Tests.Features.Notifications.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Notifications.Services;

/// <summary>
/// One message to each of the user's devices, encrypted and signed for real, with the push
/// service's answer deciding what becomes of each device.
/// </summary>
public sealed class WebPushReleaseNotifierTests : IDisposable
{
    private const string Phone = "https://fcm.googleapis.com/fcm/send/phone";
    private const string Laptop = "https://updates.push.services.mozilla.com/wpush/v2/laptop";

    private static readonly ReleaseNotification Notification = new("1 new volume out", "86 Vol 14", "/", "lnshelf-releases");

    private readonly NotificationsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task NotifyAsync_EveryDeviceAccepts_DeliversToEachAndRecordsTheSuccess()
    {
        await AddDevicesAsync(Phone, Laptop);

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(new NotificationDelivery(Devices: 2, Delivered: 2, FailedTransiently: 0, Removed: 0));
        _harness.PushService.Requests.Select(request => request.RequestUri!.ToString())
            .ShouldBe([Phone, Laptop], ignoreOrder: true);

        // Encrypted (RFC 8291) and signed (RFC 8292): the push service never sees the titles.
        var request = _harness.PushService.Requests[0];
        request.Content!.Headers.ContentEncoding.ShouldContain("aes128gcm");
        request.Headers.Authorization!.Scheme.ShouldBe("vapid");

        await using var check = _harness.CreateDbContext();
        (await check.PushSubscriptions.ToListAsync()).ShouldAllBe(device => device.LastSuccessAt == _harness.Time.GetUtcNow());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task NotifyAsync_ADeviceThePushServiceCallsGone_IsForgotten(HttpStatusCode status)
    {
        await AddDevicesAsync(Phone, Laptop);
        _harness.PushService.Answer(Phone, status);

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(new NotificationDelivery(Devices: 2, Delivered: 1, FailedTransiently: 0, Removed: 1));

        await using var check = _harness.CreateDbContext();
        (await check.PushSubscriptions.SingleAsync()).Endpoint.ShouldBe(Laptop);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 1)]
    [InlineData(HttpStatusCode.TooManyRequests, 1)]
    [InlineData(HttpStatusCode.Forbidden, 0)]
    [InlineData(HttpStatusCode.BadRequest, 0)]
    public async Task NotifyAsync_ARefusal_CountsAFailureAndKeepsTheDevice(HttpStatusCode status, int transient)
    {
        await AddDevicesAsync(Phone);
        _harness.PushService.Answer(Phone, status);

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        // Only an answer worth waiting out (5xx, 429) asks for a retry; a 403 or 400 would
        // only be refused again.
        delivery.ShouldBe(new NotificationDelivery(Devices: 1, Delivered: 0, FailedTransiently: transient, Removed: 0));
        delivery.ShouldRetry.ShouldBe(transient > 0);

        await using var check = _harness.CreateDbContext();
        (await check.PushSubscriptions.SingleAsync()).FailureCount.ShouldBe(1);
    }

    // A rotated VAPID key makes every old subscription answer 403 forever.
    [Fact]
    public async Task NotifyAsync_ADeviceRefusedFiveTimesInARow_IsForgotten()
    {
        await using (var seed = _harness.CreateDbContext())
        {
            var refusing = NotificationsHarness.Device(Phone);
            refusing.FailureCount = 4;
            seed.PushSubscriptions.Add(refusing);
            await seed.SaveChangesAsync();
        }

        _harness.PushService.Answer(Phone, HttpStatusCode.Forbidden);

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(new NotificationDelivery(Devices: 1, Delivered: 0, FailedTransiently: 0, Removed: 1));

        await using var check = _harness.CreateDbContext();
        (await check.PushSubscriptions.CountAsync()).ShouldBe(0);
    }

    public static TheoryData<Exception> NoAnswer => new()
    {
        new HttpRequestException("Connection refused"),
        // HttpClient's own timeout: cancelled, but not by the caller's token.
        new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"),
    };

    [Theory]
    [MemberData(nameof(NoAnswer))]
    public async Task NotifyAsync_NoAnswerFromThePushService_IsTransientAndKeepsTheDevice(Exception failure)
    {
        await AddDevicesAsync(Phone);
        _harness.PushService.Fail(Phone, failure);

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(new NotificationDelivery(Devices: 1, Delivered: 0, FailedTransiently: 1, Removed: 0));
        delivery.ShouldRetry.ShouldBeTrue();

        await using var check = _harness.CreateDbContext();
        (await check.PushSubscriptions.SingleAsync()).FailureCount.ShouldBe(1);
    }

    [Fact]
    public async Task NotifyAsync_AStoredKeyThatIsNoPoint_CostsOnlyThatDevice()
    {
        // 65 bytes with the right marker that are not on the P-256 curve: what a buggy or
        // hostile client could have stored before the subscribe check looked at the curve.
        await using (var seed = _harness.CreateDbContext())
        {
            var broken = NotificationsHarness.Device(Phone);
            broken.P256dh = Base64Url.EncodeToString([0x04, .. Enumerable.Repeat((byte)1, 64)]);
            seed.PushSubscriptions.Add(broken);
            seed.PushSubscriptions.Add(NotificationsHarness.Device(Laptop));
            await seed.SaveChangesAsync();
        }

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(new NotificationDelivery(Devices: 2, Delivered: 1, FailedTransiently: 0, Removed: 0));

        await using var check = _harness.CreateDbContext();
        var devices = await check.PushSubscriptions.ToDictionaryAsync(device => device.Endpoint);
        devices[Phone].FailureCount.ShouldBe(1);
        devices[Laptop].LastSuccessAt.ShouldBe(_harness.Time.GetUtcNow());
    }

    [Fact]
    public async Task NotifyAsync_AnotherUsersDevice_IsNeverSentTo()
    {
        await using (var someoneElse = _harness.CreateDbContext(TestUsers.SomeoneElse))
        {
            someoneElse.PushSubscriptions.Add(NotificationsHarness.Device(Laptop));
            await someoneElse.SaveChangesAsync();
        }

        await using var dbContext = _harness.CreateDbContext();
        var delivery = await _harness.CreateNotifier(dbContext).NotifyAsync(Notification, CancellationToken.None);

        delivery.ShouldBe(NotificationDelivery.Nowhere);
        _harness.PushService.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_WebPushNotConfigured_SendsNothing()
    {
        using var unconfigured = new NotificationsHarness(webPushConfigured: false);
        await using (var seed = unconfigured.CreateDbContext())
        {
            seed.PushSubscriptions.Add(NotificationsHarness.Device(Phone));
            await seed.SaveChangesAsync();
        }

        await using var dbContext = unconfigured.CreateDbContext();
        var notifier = unconfigured.CreateNotifier(dbContext);

        notifier.IsAvailable.ShouldBeFalse();
        (await notifier.NotifyAsync(Notification, CancellationToken.None)).ShouldBe(NotificationDelivery.Nowhere);
        unconfigured.PushService.Requests.ShouldBeEmpty();
    }

    private async Task AddDevicesAsync(params string[] endpoints)
    {
        await using var dbContext = _harness.CreateDbContext();
        foreach (var endpoint in endpoints)
        {
            dbContext.PushSubscriptions.Add(NotificationsHarness.Device(endpoint));
        }

        await dbContext.SaveChangesAsync();
    }
}
