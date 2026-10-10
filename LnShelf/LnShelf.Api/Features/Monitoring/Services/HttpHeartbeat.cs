namespace LnShelf.Api.Features.Monitoring.Services;

/// <summary>
/// Beats by calling the monitor's push URL, Uptime Kuma style:
/// <c>GET &lt;url&gt;?status=up|down&amp;msg=…</c>.
/// </summary>
public sealed class HttpHeartbeat(
    HttpClient httpClient,
    IOptions<HeartbeatOptions> options,
    ILogger<HttpHeartbeat> logger) : IHeartbeat
{
    public async Task BeatAsync(
        string monitor,
        HeartbeatOutcome outcome,
        string message,
        CancellationToken cancellationToken)
    {
        // No URL for the monitor is a normal state (development, tests, a server without Kuma),
        // not a fault. Null too: compose passes an unset .env value through as empty, and the
        // binder turns that into a missing Uri rather than an error.
        if (!options.Value.Monitors.TryGetValue(monitor, out var pushUrl) || pushUrl is null)
        {
            return;
        }

        try
        {
            using var response = await httpClient.GetAsync(PushUri(pushUrl, outcome, message), cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Swallowed on purpose (IHeartbeat's contract): the job already did its work, and
            // the monitor notices the missing beat on its own. The URL is not logged; its path
            // is the push token.
            logger.LogWarning(exception, "Could not send the {Monitor} heartbeat", monitor);
        }
    }

    /// <summary>The push URL with its query replaced by this beat's status and message.</summary>
    // Replaced rather than appended: Uptime Kuma shows the push URL with a sample query
    // (?status=up&msg=OK&ping=), and a pasted copy of it must not send that instead.
    public static Uri PushUri(Uri pushUrl, HeartbeatOutcome outcome, string message)
    {
        ArgumentNullException.ThrowIfNull(pushUrl);

        var status = outcome is HeartbeatOutcome.Up ? "up" : "down";

        return new UriBuilder(pushUrl)
        {
            Query = $"status={status}&msg={Uri.EscapeDataString(message)}",
        }.Uri;
    }
}
