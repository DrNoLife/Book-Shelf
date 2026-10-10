using System.Threading.Channels;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Runs "Sync now" in the background, so the request that asked for it can return at once.
/// </summary>
// A whole-shelf sync takes minutes, and run inside the request it outlived nginx's 300s and
// Cloudflare's 100s timeouts — and a browser giving up cancelled the run with it, through
// RequestAborted. Here the run belongs to the host: its own scope, acting as the user who
// asked (CLAUDE.md — background work calls ActAs in its own scope), cancelled only by
// shutdown.
//
// One run at a time, in arrival order. The metadata sources are shared, rate-limited and
// donated, so two people's syncs going at once would finish no sooner overall; a queued
// request already holds its user's SyncRunGate lease, so asking twice is still refused.
public sealed class ManualSyncRunner(
    IServiceScopeFactory scopeFactory,
    SyncRunGate gate,
    ILogger<ManualSyncRunner> logger) : BackgroundService
{
    private readonly Channel<ManualSyncRequest> _requests =
        Channel.CreateUnbounded<ManualSyncRequest>(new UnboundedChannelOptions { SingleReader = true });

    /// <returns>
    /// False when a sync for that user is already running or queued, nightly or manual.
    /// </returns>
    public bool TryStart(Guid userId)
    {
        var lease = gate.TryEnter(userId);
        if (lease is null)
        {
            return false;
        }

        // An unbounded channel only refuses a write once it has been completed, which nothing
        // here does — but a lease that is never handed on must not stay taken.
        if (!_requests.Writer.TryWrite(new ManualSyncRequest(userId, lease)))
        {
            lease.Dispose();
            return false;
        }

        logger.LogInformation("Manual sync queued for user {UserId}", userId);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(stoppingToken))
            {
                using (request.Lease)
                {
                    await RunAsync(request.UserId, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Deliberately empty: the host is stopping, and an interrupted run has already
            // written its own ending (SeriesSyncService).
        }
    }

    private async Task RunAsync(Guid userId, CancellationToken stoppingToken)
    {
        // A scope per run: the sync service is scoped because the DbContext is.
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);
        var sync = scope.ServiceProvider.GetRequiredService<ISeriesSyncService>();

        try
        {
            await sync.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Swallowed on purpose: nobody is waiting on this run any more, and an exception
            // escaping here would end the loop and every later "Sync now" with it. The run
            // already isolates per-series failures; this is a failure of the run itself.
            logger.LogError(exception, "The manual sync run for user {UserId} failed outright", userId);
        }
    }

    private sealed record ManualSyncRequest(Guid UserId, IDisposable Lease);
}
