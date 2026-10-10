using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Monitoring.Models;

/// <summary>
/// Where each monitor's push URL is, bound once through <c>IOptions&lt;T&gt;</c>.
/// </summary>
// A map rather than one property per job, so a second monitored job is a new key in the
// server's .env and a constant in HeartbeatMonitors, not a change here. Empty by default: dev
// and tests beat for nobody. The URLs carry Uptime Kuma's push tokens, so they are secrets and
// arrive as environment variables (Heartbeat__Monitors__NightlySync).
public sealed class HeartbeatOptions
{
    public const string SectionName = "Heartbeat";

    public Dictionary<string, Uri> Monitors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [Range(typeof(TimeSpan), "00:00:01", "00:01:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
