namespace LnShelf.Api.Features.Monitoring.Abstractions;

/// <summary>
/// The named seam for telling a monitor that a scheduled job ran (Uptime Kuma's push monitors
/// today). A job that stops running makes no noise of its own; a missed beat is what raises
/// the alarm.
/// </summary>
// Best effort by contract: an implementation never throws for a monitor that is down or
// unreachable, because a monitoring outage must not fail the job it watches.
public interface IHeartbeat
{
    /// <param name="monitor">Which monitor, one of <see cref="Models.HeartbeatMonitors"/>.</param>
    /// <param name="message">Counts only. It leaves the server, so never titles, names or emails.</param>
    Task BeatAsync(string monitor, HeartbeatOutcome outcome, string message, CancellationToken cancellationToken);
}
