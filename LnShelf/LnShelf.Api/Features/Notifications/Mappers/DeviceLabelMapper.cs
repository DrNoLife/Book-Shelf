namespace LnShelf.Api.Features.Notifications.Mappers;

/// <summary>
/// Names a device from its user agent, for the profile's device list: "Chrome on Android".
/// </summary>
// Read once, when the device subscribes, and stored as the label; the user agent itself is
// not kept. A guess that is good enough to tell a phone from a laptop is all it needs to be.
public static class DeviceLabelMapper
{
    // Order matters: Edge, Opera and Samsung all say "Chrome", and every Chromium browser and
    // Chrome itself say "Safari".
    private static readonly (string Marker, string Name)[] Browsers =
    [
        ("Edg/", "Edge"),
        ("EdgA/", "Edge"),
        ("EdgiOS/", "Edge"),
        ("OPR/", "Opera"),
        ("SamsungBrowser/", "Samsung Internet"),
        ("FxiOS/", "Firefox"),
        ("Firefox/", "Firefox"),
        ("CriOS/", "Chrome"),
        ("Chrome/", "Chrome"),
        ("Safari/", "Safari"),
    ];

    // iPhone and iPad before "Mac OS X", which both of them also say; Android before Linux.
    private static readonly (string Marker, string Name)[] Platforms =
    [
        ("iPhone", "iPhone"),
        ("iPad", "iPad"),
        ("Android", "Android"),
        ("Windows", "Windows"),
        ("CrOS", "ChromeOS"),
        ("Macintosh", "Mac"),
        ("Linux", "Linux"),
    ];

    /// <returns>Null when the user agent names neither a browser nor a platform this knows.</returns>
    public static string? FromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var browser = FirstMatch(Browsers, userAgent);
        var platform = FirstMatch(Platforms, userAgent);

        // Built only from the names above, never from the user agent's own text, so it always
        // fits PushSubscription.MaximumDeviceLabelLength.
        return (browser, platform) switch
        {
            (null, null) => null,
            ({ } name, null) => name,
            (null, { } name) => name,
            ({ } browserName, { } platformName) => $"{browserName} on {platformName}",
        };
    }

    private static string? FirstMatch((string Marker, string Name)[] candidates, string userAgent) =>
        candidates.FirstOrDefault(candidate => userAgent.Contains(candidate.Marker, StringComparison.Ordinal)).Name;
}
