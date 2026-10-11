namespace LnShelf.Api.Features.Releases.Abstractions;

/// <summary>
/// Tells the current user about releases (feature-spec §4.5). Web Push today; ntfy, Discord or
/// email would each be another implementation, not an edit.
/// </summary>
// Addressed to the current user (ICurrentUser) rather than to a device, a topic or an address:
// where a message goes is the implementation's business, and the notification pass only needs
// to know whether it got there.
public interface IReleaseNotifier
{
    /// <summary>False when this notifier is not configured on this server.</summary>
    bool IsAvailable { get; }

    /// <summary>Sends one notification to every place the current user receives them.</summary>
    /// <remarks>Throws only for cancellation; a refused delivery is counted, not thrown.</remarks>
    Task<NotificationDelivery> NotifyAsync(ReleaseNotification notification, CancellationToken cancellationToken);
}
