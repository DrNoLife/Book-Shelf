namespace LnShelf.Api.Features.Notifications.Models;

/// <summary>
/// One device a user turned notifications on for: the browser's Web Push subscription.
/// </summary>
// A root of its own (IOwned) rather than a child of anything: it belongs to the user, not to
// a series, and it is what the notification pass reads per user.
//
// Named like the browser's own PushSubscription on purpose. Lib.Net.Http.WebPush has a type
// of the same name, so the notifier that maps this to that one aliases the library's.
public sealed class PushSubscription : IOwned
{
    /// <summary>
    /// The endpoint's width. Real ones are a few hundred characters; the cap keeps the unique
    /// index well inside what a Postgres btree entry can hold.
    /// </summary>
    public const int MaximumEndpointLength = 1000;

    /// <summary>The device label's width, shared by its configuration and the writer.</summary>
    public const int MaximumDeviceLabelLength = 100;

    public Guid Id { get; set; }

    /// <summary>The user this belongs to (IOwned). Stamped on save.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The push service's URL for this device. Secret-ish: whoever has it can push to it.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The browser's public key for payload encryption, base64url.</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>The browser's authentication secret for payload encryption, base64url.</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>What the profile page calls this device, from its user agent ("Chrome on Android").</summary>
    public string? DeviceLabel { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The last push the push service accepted. Null until one has.</summary>
    public DateTimeOffset? LastSuccessAt { get; set; }

    /// <summary>Consecutive failed deliveries, reset by a success.</summary>
    public int FailureCount { get; set; }
}
