using Lib.Net.Http.WebPush.Authentication;

namespace LnShelf.Api.Features.Notifications.Services;

/// <summary>
/// The server's one VAPID identity, or nothing when Web Push is switched off.
/// </summary>
// A singleton because the keys are the server's, fixed for the life of the process: parsed
// and checked once at start rather than per push. Wrapped rather than registered directly
// because there is no instance at all when the keys are unset, and the container cannot hand
// out "none".
public sealed class WebPushAuthentication : IDisposable
{
    public WebPushAuthentication(IOptions<WebPushOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        if (!value.IsConfigured)
        {
            return;
        }

        PublicKey = value.PublicKey.Trim();
        Vapid = new VapidAuthentication(PublicKey, value.PrivateKey.Trim()) { Subject = value.Subject.Trim() };
    }

    /// <summary>The key browsers subscribe with. Null when Web Push is off.</summary>
    public string? PublicKey { get; }

    /// <summary>Signs each push. Null when Web Push is off.</summary>
    public VapidAuthentication? Vapid { get; }

    public bool IsAvailable => Vapid is not null;

    public void Dispose() => Vapid?.Dispose();
}
