namespace LnShelf.Api.Features.Notifications.Abstractions;

/// <summary>
/// The notification pass after a user's sync run: sends what the run logged as one digest,
/// then prunes the activity log.
/// </summary>
public interface IReleaseNotificationDispatcher
{
    /// <summary>
    /// Runs the pass for the current user. Best effort: never throws, except when
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    Task DispatchAsync(CancellationToken cancellationToken);
}
