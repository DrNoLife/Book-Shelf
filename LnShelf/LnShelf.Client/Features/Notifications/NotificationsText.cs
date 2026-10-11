using LnShelf.Shared.Notifications;

namespace LnShelf.Client.Features.Notifications;

/// <summary>
/// The Notifications card's sentences, kept out of the markup so the rules behind them can be
/// tested without a browser.
/// </summary>
public static class NotificationsText
{
    public const string HomeScreenHint =
        "On an iPhone or iPad, add LN Shelf to your Home Screen first (Share, then Add to Home Screen), " +
        "open it from there, and turn notifications on.";

    /// <summary>
    /// Why this browser cannot turn notifications on, or null when it can.
    /// </summary>
    // iOS only offers Web Push to a Home Screen app (16.4 and later): in a Safari tab there is
    // no PushManager at all. So an Apple phone or tablet that is not standalone gets the Home
    // Screen hint whatever else it reports.
    public static string? Obstacle(PushBrowserStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.IsAppleMobile && (!status.Supported || !status.Standalone))
        {
            return HomeScreenHint;
        }

        if (!status.Supported)
        {
            return "This browser cannot receive notifications. " + HomeScreenHint;
        }

        return status.Permission == "denied"
            ? "Notifications are blocked for LN Shelf in this browser's settings. Allow them there, then come back."
            : null;
    }

    /// <summary>"Chrome on Android · on since 11 Oct 2026", plus failures when there are any.</summary>
    public static string DeviceLine(PushDeviceResponse device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var since = DateOnly.FromDateTime(device.CreatedAt.LocalDateTime).Full;
        var line = $"{device.DeviceLabel ?? "A browser"} · on since {since}";

        return device.FailureCount switch
        {
            0 => line,
            1 => $"{line} · the last delivery failed",
            var failures => $"{line} · the last {failures} deliveries failed",
        };
    }

    /// <summary>What a refused Enable says.</summary>
    public static string EnableRefused(string? error) => error switch
    {
        "denied" => "Notifications are blocked for LN Shelf in this browser's settings. Allow them there, then try again.",
        "default" => "Notifications stay off: the browser did not get an answer to its question.",
        _ => "This browser could not turn notifications on. Try again in a moment.",
    };

    public static string TestResult(PushTestResponse result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return (result.Devices, result.Delivered) switch
        {
            (0, _) => "No device has notifications on.",
            (var devices, var delivered) when delivered == devices =>
                devices == 1 ? "Sent. It should show up in a moment." : $"Sent to all {devices} devices.",
            (_, 0) => "No device took it. Try again later.",
            (var devices, var delivered) => $"Sent to {delivered} of {devices} devices.",
        };
    }
}
