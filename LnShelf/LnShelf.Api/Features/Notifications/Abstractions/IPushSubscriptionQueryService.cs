namespace LnShelf.Api.Features.Notifications.Abstractions;

/// <summary>Reads the signed-in user's notification setup for the profile card.</summary>
public interface IPushSubscriptionQueryService
{
    /// <summary>Whether the server can push, the key to subscribe with, and the devices that are on.</summary>
    Task<NotificationsResponse> GetAsync(CancellationToken cancellationToken);
}
