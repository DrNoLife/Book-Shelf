using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using WebPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace LnShelf.Api.Features.Notifications.Services;

/// <summary>
/// The first <see cref="IReleaseNotifier"/>: Web Push to every device the current user turned
/// notifications on for (feature-spec §4.5, decision D1).
/// </summary>
// Web Push over ntfy because the shelf's readers are on phones and need no app or topic for
// it; the encryption and VAPID signing are Lib.Net.Http.WebPush's, not hand-rolled.
//
// The payload passes through Google's, Mozilla's, Microsoft's or Apple's push service on its
// way to the device. It is encrypted end to end (RFC 8291), so they see its size and timing,
// not the series titles in it.
public sealed class WebPushReleaseNotifier(
    LnShelfDbContext dbContext,
    PushServiceClient pushServiceClient,
    WebPushAuthentication authentication,
    TimeProvider timeProvider,
    ILogger<WebPushReleaseNotifier> logger) : IReleaseNotifier
{
    /// <summary>How long a push service holds a message for a device that is off.</summary>
    // A day: a release digest is still worth reading the morning after, and stale after that.
    private static readonly TimeSpan TimeToLive = TimeSpan.FromDays(1);

    /// <summary>Refusals in a row after which a device is forgotten, as if it were gone.</summary>
    // A refusal is not retried, but it does not prove the device is dead either: one bad
    // message would be. Five in a row does — the usual cause is a rotated VAPID key, under
    // which every old subscription answers 403 forever — and keeping the row would keep its
    // endpoint and keys past the retention the privacy page states.
    private const int RemoveAfterRefusals = 5;

    public bool IsAvailable => authentication.IsAvailable;

    public async Task<NotificationDelivery> NotifyAsync(ReleaseNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (authentication.Vapid is not { } vapid)
        {
            return NotificationDelivery.Nowhere;
        }

        var devices = await dbContext.PushSubscriptions.ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return NotificationDelivery.Nowhere;
        }

        var payload = new WebPushPayload
        {
            Title = notification.Title,
            Body = notification.Body,
            Url = notification.Url,
            Tag = notification.Tag,
        };

        // camelCase, which is what push-worker.js reads.
        var message = new PushMessage(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web))
        {
            TimeToLive = (int)TimeToLive.TotalSeconds,
            Urgency = PushMessageUrgency.Normal,
        };

        int delivered = 0, failedTransiently = 0, removed = 0;

        foreach (var device in devices)
        {
            switch (await SendAsync(device, message, vapid, cancellationToken))
            {
                case DeviceOutcome.Delivered:
                    device.LastSuccessAt = timeProvider.GetUtcNow();
                    device.FailureCount = 0;
                    delivered++;
                    break;

                case DeviceOutcome.Gone:
                    dbContext.PushSubscriptions.Remove(device);
                    removed++;
                    break;

                case DeviceOutcome.FailedTransiently:
                    device.FailureCount++;
                    failedTransiently++;
                    break;

                case DeviceOutcome.Refused:
                    device.FailureCount++;
                    if (device.FailureCount >= RemoveAfterRefusals)
                    {
                        logger.LogInformation(
                            "Push subscription {SubscriptionId} refused {FailureCount} times in a row; removed",
                            device.Id, device.FailureCount);
                        dbContext.PushSubscriptions.Remove(device);
                        removed++;
                    }

                    break;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NotificationDelivery(devices.Count, delivered, failedTransiently, removed);
    }

    // Qualified because the library's namespace brings a PushSubscription of its own (the
    // wire shape, aliased WebPushSubscription above); this one is the stored row.
    private async Task<DeviceOutcome> SendAsync(
        Models.PushSubscription device,
        PushMessage message,
        VapidAuthentication vapid,
        CancellationToken cancellationToken)
    {
        var subscription = new WebPushSubscription { Endpoint = device.Endpoint };
        subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);

        try
        {
            await pushServiceClient.RequestPushMessageDeliveryAsync(
                subscription, message, vapid, VapidAuthenticationScheme.Vapid, cancellationToken);

            return DeviceOutcome.Delivered;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (PushServiceClientException exception)
            when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // The browser unsubscribed, or the user cleared the site's data: this device will
            // never accept another push, so it is forgotten rather than retried forever.
            logger.LogInformation("Push subscription {SubscriptionId} is gone ({StatusCode}); removed",
                device.Id, (int)exception.StatusCode);

            return DeviceOutcome.Gone;
        }
        catch (PushServiceClientException exception)
            when ((int)exception.StatusCode >= 500 || exception.StatusCode is HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Push service unavailable for subscription {SubscriptionId} ({StatusCode})",
                device.Id, (int)exception.StatusCode);

            return DeviceOutcome.FailedTransiently;
        }
        catch (PushServiceClientException exception)
        {
            // A 400, 403 or 413: something about this message or this server's keys, which a
            // retry would only repeat. The body says which (BadJwtToken, for one).
            logger.LogWarning("Push service refused subscription {SubscriptionId} ({StatusCode}): {Reason}",
                device.Id, (int)exception.StatusCode, exception.Body);

            return DeviceOutcome.Refused;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // No answer at all — the network, or the client's timeout.
            logger.LogWarning(exception, "Could not reach the push service for subscription {SubscriptionId}", device.Id);

            return DeviceOutcome.FailedTransiently;
        }
        catch (Exception exception)
        {
            // Anything else is about this one row (a stored key the library cannot import, for
            // one): it costs that device only, so the rest of the user's devices still get the
            // digest and the outcomes of those already sent are saved, not resent tomorrow.
            logger.LogWarning(exception, "Could not send to push subscription {SubscriptionId}", device.Id);

            return DeviceOutcome.Refused;
        }
    }

    private enum DeviceOutcome
    {
        Delivered,
        Gone,
        FailedTransiently,
        Refused,
    }

    /// <summary>What push-worker.js reads. Kept small: push services cap a payload at about 4 KB.</summary>
    private sealed class WebPushPayload
    {
        public required string Title { get; init; }
        public required string Body { get; init; }
        public required string Url { get; init; }
        public required string Tag { get; init; }
    }
}
