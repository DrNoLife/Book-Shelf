namespace LnShelf.Api.Features.Notifications.Services;

/// <inheritdoc />
// The endpoint is never logged: it is the address a push to that device goes to.
public sealed class PushSubscriptionCommandService(
    LnShelfDbContext dbContext,
    WebPushAuthentication authentication,
    IReleaseNotifier notifier,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<PushSubscriptionCommandService> logger) : IPushSubscriptionCommandService
{
    /// <summary>
    /// More than anyone has browsers, and few enough that one account cannot make every
    /// sync pass send hundreds of pushes.
    /// </summary>
    public const int MaximumDevicesPerUser = 20;

    private const string NotSetUp = "Notifications are not set up on this server.";

    public async Task<PushDeviceResponse> SubscribeAsync(
        CreatePushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!authentication.IsAvailable)
        {
            throw new ConflictException(NotSetUp);
        }

        var endpoint = request.Endpoint.Trim();
        if (!PushEndpointPolicy.IsAllowed(endpoint))
        {
            throw new InvalidInputException("That is not a push service this shelf sends notifications through.");
        }

        if (!VapidKeys.IsPublicKey(request.P256dh)
            || !VapidKeys.DecodesTo(request.Auth, VapidKeys.AuthenticationSecretLength))
        {
            throw new InvalidInputException("The browser's notification keys are not valid. Try turning notifications on again.");
        }

        // An upsert on (owner, endpoint): the same browser subscribing again after a key
        // change, or after its keys were rotated by the browser, keeps one row. The owner
        // filter is what makes it per owner — a second account on the same browser has a row
        // of its own, which the unique index allows.
        var subscription = await dbContext.PushSubscriptions
            .FirstOrDefaultAsync(candidate => candidate.Endpoint == endpoint, cancellationToken);

        if (subscription is null)
        {
            if (await dbContext.PushSubscriptions.CountAsync(cancellationToken) >= MaximumDevicesPerUser)
            {
                throw new ConflictException(
                    $"Notifications are on for {MaximumDevicesPerUser} devices already. Remove one first.");
            }

            subscription = new PushSubscription
            {
                Id = Guid.CreateVersion7(),
                Endpoint = endpoint,
                CreatedAt = timeProvider.GetUtcNow(),
            };
            dbContext.PushSubscriptions.Add(subscription);
        }

        subscription.P256dh = request.P256dh.Trim();
        subscription.Auth = request.Auth.Trim();
        subscription.DeviceLabel = DeviceLabelMapper.FromUserAgent(userAgent);
        subscription.FailureCount = 0;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Notifications turned on for {DeviceLabel} for user {UserId}",
            subscription.DeviceLabel ?? "an unnamed device",
            currentUser.Id);

        return PushDeviceMapper.ToResponse(subscription);
    }

    public async Task<bool> RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.PushSubscriptions
            .FirstOrDefaultAsync(candidate => candidate.Id == subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        dbContext.PushSubscriptions.Remove(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Notifications turned off for {DeviceLabel} for user {UserId}",
            subscription.DeviceLabel ?? "an unnamed device",
            currentUser.Id);

        return true;
    }

    public async Task<PushTestResponse> SendTestAsync(CancellationToken cancellationToken)
    {
        if (!notifier.IsAvailable)
        {
            throw new ConflictException(NotSetUp);
        }

        // Through the real notifier, so a test that arrives proves the path a release takes.
        var delivery = await notifier.NotifyAsync(
            new ReleaseNotification(
                "Notifications are on",
                "New volumes and release dates on your shelf will show up like this.",
                "/profile",
                "lnshelf-test"),
            cancellationToken);

        return new PushTestResponse { Devices = delivery.Devices, Delivered = delivery.Delivered };
    }
}
