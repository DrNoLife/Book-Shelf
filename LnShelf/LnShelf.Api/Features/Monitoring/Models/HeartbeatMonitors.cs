namespace LnShelf.Api.Features.Monitoring.Models;

/// <summary>
/// The monitors the API beats for, as keys of <see cref="HeartbeatOptions.Monitors"/>.
/// </summary>
public static class HeartbeatMonitors
{
    /// <summary>The nightly sync (<c>Heartbeat__Monitors__NightlySync</c>).</summary>
    public const string NightlySync = "NightlySync";
}
