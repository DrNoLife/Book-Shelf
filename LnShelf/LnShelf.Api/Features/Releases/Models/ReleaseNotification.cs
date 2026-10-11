namespace LnShelf.Api.Features.Releases.Models;

/// <summary>One message to a reader, in any notifier's terms.</summary>
/// <param name="Title">The headline: "2 new volumes out".</param>
/// <param name="Body">The detail: "86 Vol 14, Overlord Vol 17".</param>
/// <param name="Url">The app path a click opens, always on the shelf's own origin: "/" or "/series/{id}".</param>
/// <param name="Tag">
/// Replaces an earlier notification with the same tag rather than stacking under it, so two
/// syncs in a row leave one message on the lock screen.
/// </param>
public sealed record ReleaseNotification(string Title, string Body, string Url, string Tag);

/// <summary>What became of one notification across a user's devices.</summary>
/// <param name="Devices">How many places it was sent to.</param>
/// <param name="Delivered">Accepted by the push service.</param>
/// <param name="FailedTransiently">A 5xx, 429 or network failure: worth trying again later.</param>
/// <param name="Removed">Gone for good (404, 410), and forgotten.</param>
public sealed record NotificationDelivery(int Devices, int Delivered, int FailedTransiently, int Removed)
{
    public static NotificationDelivery Nowhere { get; } = new(0, 0, 0, 0);

    /// <summary>
    /// Nothing got through and something still might: the events stay pending for the next pass.
    /// </summary>
    // Not "every device failed transiently" to the letter: a device the push service has just
    // said is gone cannot receive the retry either, so it does not stand in the way of one for
    // a phone that was merely unreachable.
    public bool ShouldRetry => Delivered == 0 && FailedTransiently > 0;
}
