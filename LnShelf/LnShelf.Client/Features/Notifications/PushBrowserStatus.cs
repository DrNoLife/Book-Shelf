namespace LnShelf.Client.Features.Notifications;

/// <summary>What <c>lnshelf.pushStatus</c> reports about this browser.</summary>
// A class, as the shapes that cross a wire are: this one crosses the JS interop boundary.
public sealed class PushBrowserStatus
{
    /// <summary>Service workers, PushManager and Notification are all there.</summary>
    public bool Supported { get; init; }

    /// <summary>Running as an installed app (Home Screen), not in a browser tab.</summary>
    public bool Standalone { get; init; }

    /// <summary>An iPhone or iPad, where push needs the app on the Home Screen first.</summary>
    public bool IsAppleMobile { get; init; }

    /// <summary><c>default</c>, <c>granted</c>, <c>denied</c>, or <c>unsupported</c>.</summary>
    public string Permission { get; init; } = "default";

    /// <summary>This browser's current subscription endpoint, or null when it has none.</summary>
    public string? Endpoint { get; init; }

    /// <summary>The subscription was made with the server's current key.</summary>
    public bool KeyMatches { get; init; }
}

/// <summary>What <c>lnshelf.subscribePush</c> answers: the subscription, or why there is none.</summary>
public sealed class BrowserPushSubscription
{
    public string? Endpoint { get; init; }
    public string? P256dh { get; init; }
    public string? Auth { get; init; }

    /// <summary><c>denied</c> or <c>default</c> when permission was not given; <c>failed</c> otherwise.</summary>
    public string? Error { get; init; }
}
