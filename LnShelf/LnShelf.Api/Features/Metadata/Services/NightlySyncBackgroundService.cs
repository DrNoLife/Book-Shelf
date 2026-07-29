namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The nightly refresh: a <c>BackgroundService</c>, not a job framework
/// (CLAUDE.md — one nightly loop does not justify Hangfire).
/// </summary>
public sealed class NightlySyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<NightlySyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            // Off in development by default: a job that hits two community APIs
            // every time someone runs the app is not a polite job.
            logger.LogInformation("Nightly sync is disabled; POST /api/sync runs it on demand.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun();
            logger.LogInformation(
                "Next sync at {SyncHour}:00 local, in {DelayHours:F1}h", options.Value.Hour, delay.TotalHours);

            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        // A scope per run: the sync service is scoped because the DbContext is.
        await using var scope = scopeFactory.CreateAsyncScope();
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
            // The run already isolates per-series failures; this catches a failure of
            // the run itself. Swallowed on purpose — the loop has to survive to
            // try again tomorrow.
            logger.LogError(exception, "The nightly sync run failed outright");
        }
    }

    private TimeSpan TimeUntilNextRun()
    {
        var now = timeProvider.GetLocalNow();
        var todaysRun = new DateTimeOffset(
            now.Year, now.Month, now.Day, options.Value.Hour, 0, 0, now.Offset);

        return todaysRun > now ? todaysRun - now : todaysRun.AddDays(1) - now;
    }
}
