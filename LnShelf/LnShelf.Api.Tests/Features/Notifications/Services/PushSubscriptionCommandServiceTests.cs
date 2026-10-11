using LnShelf.Api.Features.Notifications.Services;
using LnShelf.Api.Tests.Features.Notifications.Fixtures;
using LnShelf.Shared.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Notifications.Services;

/// <summary>
/// Turning a device on: an upsert per (user, endpoint), refused for anything but a known push
/// service, and refused outright when the server has no keys.
/// </summary>
public sealed class PushSubscriptionCommandServiceTests : IDisposable
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/phone";
    private const string AndroidChrome =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36";

    private readonly NotificationsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task SubscribeAsync_ANewDevice_IsSavedWithItsLabel()
    {
        var device = await SubscribeAsync(Request(Endpoint));

        device.Endpoint.ShouldBe(Endpoint);
        device.DeviceLabel.ShouldBe("Chrome on Android");
        device.CreatedAt.ShouldBe(_harness.Time.GetUtcNow());

        await using var dbContext = _harness.CreateDbContext();
        (await dbContext.PushSubscriptions.SingleAsync()).OwnerId.ShouldBe(TestUsers.Owner);
    }

    // The same browser subscribing again (new keys after a key rotation, or Enable pressed
    // twice) keeps one row: the unique index is (owner, endpoint).
    [Fact]
    public async Task SubscribeAsync_TheSameEndpointAgain_UpdatesTheOneRow()
    {
        var first = await SubscribeAsync(Request(Endpoint));
        var replacement = Request(Endpoint);

        var second = await SubscribeAsync(replacement);

        second.Id.ShouldBe(first.Id);
        await using var dbContext = _harness.CreateDbContext();
        (await dbContext.PushSubscriptions.SingleAsync()).P256dh.ShouldBe(replacement.P256dh);
    }

    [Fact]
    public async Task SubscribeAsync_AnotherUserOnTheSameBrowser_GetsARowOfTheirOwn()
    {
        await SubscribeAsync(Request(Endpoint));
        await SubscribeAsync(Request(Endpoint), TestUsers.SomeoneElse);

        await using var dbContext = _harness.CreateDbContext(TestUsers.SomeoneElse);
        (await dbContext.PushSubscriptions.SingleAsync()).OwnerId.ShouldBe(TestUsers.SomeoneElse);
    }

    [Theory]
    [InlineData("http://api:8080/api/sync")]
    [InlineData("https://192.168.20.224:3001/api/push/token")]
    [InlineData("https://example.com/push")]
    public async Task SubscribeAsync_AnEndpointThatIsNotAPushService_IsRefused(string endpoint)
    {
        await Should.ThrowAsync<InvalidInputException>(() => SubscribeAsync(Request(endpoint)));

        await using var dbContext = _harness.CreateDbContext();
        (await dbContext.PushSubscriptions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SubscribeAsync_KeysThatAreNotKeys_AreRefused() =>
        await Should.ThrowAsync<InvalidInputException>(() =>
            SubscribeAsync(Request(Endpoint) with { P256dh = "nope", Auth = "nope" }));

    [Fact]
    public async Task SubscribeAsync_WebPushNotConfigured_IsAConflict()
    {
        using var unconfigured = new NotificationsHarness(webPushConfigured: false);
        await using var dbContext = unconfigured.CreateDbContext();

        await Should.ThrowAsync<ConflictException>(() =>
            CreateService(dbContext, unconfigured).SubscribeAsync(Request(Endpoint), AndroidChrome, CancellationToken.None));
    }

    [Fact]
    public async Task SubscribeAsync_PastTheDeviceLimit_IsAConflict()
    {
        for (var index = 0; index < PushSubscriptionCommandService.MaximumDevicesPerUser; index++)
        {
            await SubscribeAsync(Request($"{Endpoint}-{index}"));
        }

        await Should.ThrowAsync<ConflictException>(() => SubscribeAsync(Request($"{Endpoint}-one-too-many")));
    }

    [Fact]
    public async Task RemoveAsync_AnotherUsersDevice_IsNotFound()
    {
        var theirs = await SubscribeAsync(Request(Endpoint), TestUsers.SomeoneElse);

        await using var dbContext = _harness.CreateDbContext();
        (await CreateService(dbContext, _harness).RemoveAsync(theirs.Id, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveAsync_OwnDevice_DeletesIt()
    {
        var mine = await SubscribeAsync(Request(Endpoint));

        await using var dbContext = _harness.CreateDbContext();
        (await CreateService(dbContext, _harness).RemoveAsync(mine.Id, CancellationToken.None)).ShouldBeTrue();
        (await dbContext.PushSubscriptions.CountAsync()).ShouldBe(0);
    }

    private async Task<PushDeviceResponse> SubscribeAsync(CreatePushSubscriptionRequest request, Guid? userId = null)
    {
        await using var dbContext = _harness.CreateDbContext(userId);
        return await CreateService(dbContext, _harness).SubscribeAsync(request, AndroidChrome, CancellationToken.None);
    }

    private static PushSubscriptionCommandService CreateService(LnShelfDbContext dbContext, NotificationsHarness harness) =>
        new(dbContext,
            harness.Authentication,
            harness.CreateNotifier(dbContext),
            TestUsers.Current,
            harness.Time,
            NullLogger<PushSubscriptionCommandService>.Instance);

    private static CreatePushSubscriptionRequest Request(string endpoint)
    {
        var device = NotificationsHarness.Device(endpoint);
        return new CreatePushSubscriptionRequest { Endpoint = endpoint, P256dh = device.P256dh, Auth = device.Auth };
    }
}
