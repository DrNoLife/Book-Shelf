namespace LnShelf.Api.Features.Notifications.Models;

/// <summary>
/// Which push endpoints the server agrees to POST to: https, on a known browser push service.
/// </summary>
// An SSRF guard. The endpoint is a URL the user hands the server, and the server later POSTs
// to it from inside the compose network. Without this, "subscribing" http://api:8080/… or the
// LAN's Uptime Kuma would make the shelf send requests wherever a signed-in user pointed it.
//
// The four services every browser that can install the app uses: Chrome and Android (FCM),
// Firefox (Mozilla autopush), Edge and Windows (WNS) and Safari on macOS and iOS (APNs). A new
// browser vendor's push service is a line here.
public static class PushEndpointPolicy
{
    private const string GoogleHost = "fcm.googleapis.com";

    private static readonly string[] AllowedHostSuffixes =
    [
        ".push.services.mozilla.com",
        ".notify.windows.com",
        ".push.apple.com",
    ];

    public static bool IsAllowed(string? endpoint)
    {
        if (endpoint is null
            || endpoint.Length > PushSubscription.MaximumEndpointLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // The default port only, and no credentials: a push service URL has neither, and
        // either one is a way of steering the request somewhere the host name does not say.
        if (uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort
            || uri.UserInfo.Length > 0
            || uri.HostNameType is not UriHostNameType.Dns)
        {
            return false;
        }

        var host = uri.IdnHost;

        return string.Equals(host, GoogleHost, StringComparison.OrdinalIgnoreCase)
               || AllowedHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
