namespace LnShelf.Api.Features.Notifications.Abstractions;

/// <summary>Turns notifications on and off per device, and sends a test.</summary>
public interface IPushSubscriptionCommandService
{
    /// <summary>
    /// Saves this browser's subscription, or refreshes the one already saved for its endpoint.
    /// Throws <see cref="ConflictException"/> when the server cannot push or the user has too
    /// many devices, and <see cref="InvalidInputException"/> for an endpoint or key the server
    /// will not use.
    /// </summary>
    Task<PushDeviceResponse> SubscribeAsync(
        CreatePushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken);

    /// <returns>False when the user has no such device.</returns>
    Task<bool> RemoveAsync(Guid subscriptionId, CancellationToken cancellationToken);

    /// <summary>Sends "this is what it will look like" to every device. Throws <see cref="ConflictException"/> when the server cannot push.</summary>
    Task<PushTestResponse> SendTestAsync(CancellationToken cancellationToken);
}
