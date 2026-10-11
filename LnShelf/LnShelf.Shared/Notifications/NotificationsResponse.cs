namespace LnShelf.Shared.Notifications;

/// <summary>
/// The signed-in user's notification setup: whether this server can push at all, the key a
/// browser subscribes with, and the devices that are on.
/// </summary>
public sealed record NotificationsResponse
{
    /// <summary>False when the server has no Web Push keys; the client then shows nothing.</summary>
    public required bool Available { get; init; }

    /// <summary>The VAPID public key, base64url, for <c>pushManager.subscribe</c>. Null when unavailable.</summary>
    public string? PublicKey { get; init; }

    public IReadOnlyList<PushDeviceResponse> Devices { get; init; } = [];
}

/// <summary>One device notifications are on for.</summary>
public sealed record PushDeviceResponse
{
    public required Guid Id { get; init; }

    /// <summary>
    /// The browser's push endpoint, so the client can tell which row is the device it runs
    /// on. Only ever sent to the user it belongs to.
    /// </summary>
    // Safe to hand back to its owner: a push service only accepts messages for it that are
    // signed with this server's private key, which never leaves the server. The account
    // export still leaves it out, because that file is kept and passed around.
    public required string Endpoint { get; init; }

    /// <summary>"Chrome on Android". Null when the browser did not say.</summary>
    public string? DeviceLabel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastSuccessAt { get; init; }

    /// <summary>Deliveries that failed in a row since the last one that worked.</summary>
    public int FailureCount { get; init; }
}

/// <summary>A browser's <c>PushSubscription</c>, as its <c>toJSON()</c> gives it.</summary>
public sealed record CreatePushSubscriptionRequest
{
    public required string Endpoint { get; init; }

    /// <summary>The browser's P-256 public key, base64url.</summary>
    public required string P256dh { get; init; }

    /// <summary>The browser's 16-byte authentication secret, base64url.</summary>
    public required string Auth { get; init; }
}

/// <summary>What a test notification reached.</summary>
public sealed record PushTestResponse
{
    public required int Devices { get; init; }
    public required int Delivered { get; init; }
}

/// <summary>Mutes or unmutes the push notifications for one series.</summary>
public sealed record SetSeriesNotificationsRequest
{
    public required bool Muted { get; init; }
}
