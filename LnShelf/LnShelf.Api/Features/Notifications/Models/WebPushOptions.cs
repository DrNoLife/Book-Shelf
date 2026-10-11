namespace LnShelf.Api.Features.Notifications.Models;

/// <summary>
/// The server's Web Push keys, bound once through <c>IOptions&lt;T&gt;</c>. All three empty is
/// the feature switched off: the endpoints say so and the client shows nothing.
/// </summary>
// Secrets, so never in appsettings.json: the server's .env sets WebPush__PublicKey,
// WebPush__PrivateKey and WebPush__Subject (README, Server configuration), and `vapid-keys`
// prints a fresh pair. Changing the pair invalidates every subscription — the client notices
// the key changed and subscribes again.
public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    /// <summary>The VAPID public key, base64url. Browsers subscribe with it.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>The VAPID private key, base64url. Signs every push.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Who push services can contact about this sender: a <c>mailto:</c> or <c>https:</c>
    /// address. Apple refuses tokens without a real one (BadJwtToken).
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey)
        && !string.IsNullOrWhiteSpace(PrivateKey)
        && !string.IsNullOrWhiteSpace(Subject);
}
