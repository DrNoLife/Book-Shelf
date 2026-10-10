namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The nightly refresh: a <c>BackgroundService</c>, not a job framework
/// (CLAUDE.md — one nightly loop does not justify Hangfire).
/// </summary>
public sealed class NightlySyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    SyncRunGate gate,
    IOptions<SyncOptions> options,
    IOptions<OpenLibraryOptions> lookupOptions,
    TimeProvider timeProvider,
    IHeartbeat heartbeat,
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

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// One night: a run per user. Never throws, except for shutdown.
    /// </summary>
    // One run per user, each in its own scope acting as that user, so every user gets their
    // own run, discrepancies and sidebar summary, and the owner filters keep each run to that
    // user's series (feature-spec §7). The lookup allowance is the night's, shared by all.
    //
    // Public so the failure handling can be tested without waiting for 04:00.
    //
    // Ends with a heartbeat either way: up when every shelf's run completed, down when one
    // failed outright or the night never got going. Uptime Kuma alerts on a down beat and on a
    // missing one, which is the night the loop itself died.
    public async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            var budget = new LookupBudget(lookupOptions.Value.MaximumLookupsPerNight);
            int synced = 0, failed = 0;

            foreach (var userId in await UserIdsAsync(stoppingToken))
            {
                if (await RunForUserAsync(userId, budget, stoppingToken)) { synced++; } else { failed++; }
            }

            await heartbeat.BeatAsync(
                HeartbeatMonitors.NightlySync,
                failed is 0 ? HeartbeatOutcome.Up : HeartbeatOutcome.Down,
                $"{synced} shelves synced, {failed} failed",
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The database blinking at 04:00 used to escape ExecuteAsync from the user query,
            // and .NET answers an escaped BackgroundService exception by stopping the whole
            // host. Tonight is lost either way; tomorrow's run is what this keeps.
            logger.LogError(exception, "The nightly sync failed before it could finish; trying again tomorrow");

            await heartbeat.BeatAsync(
                HeartbeatMonitors.NightlySync, HeartbeatOutcome.Down, "the night failed before it finished", stoppingToken);
        }
    }

    // Most recently seen first, so when the night's allowance runs out it is the people who
    // use the shelf who got the lookups.
    private async Task<IReadOnlyList<Guid>> UserIdsAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        return await dbContext.Users
            .OrderByDescending(user => user.LastSeenAt)
            .Select(user => user.Id)
            .ToListAsync(stoppingToken);
    }

    /// <returns>False only when the run failed outright; a skipped shelf is not a failure.</returns>
    private async Task<bool> RunForUserAsync(Guid userId, LookupBudget budget, CancellationToken stoppingToken)
    {
        // Skipped rather than waited for: a "Sync now" already under way is doing tonight's
        // work for this shelf, and a second run straight after it would only repeat it.
        using var lease = gate.TryEnter(userId);
        if (lease is null)
        {
            logger.LogInformation("A sync for user {UserId} is already running; skipping tonight's", userId);
            return true;
        }

        // A scope per run: the sync service is scoped because the DbContext is.
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);
        var sync = scope.ServiceProvider.GetRequiredService<ISeriesSyncService>();

        try
        {
            await sync.RunAsync(budget, stoppingToken);

            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The run already isolates per-series failures; this catches a failure of
            // the run itself. Swallowed on purpose — one user's failure must not stop the
            // others' runs, and the loop has to survive to try again tomorrow.
            logger.LogError(exception, "The nightly sync run for user {UserId} failed outright", userId);

            return false;
        }
    }

    private TimeSpan TimeUntilNextRun() =>
        TimeUntilNextRun(timeProvider.GetUtcNow(), timeProvider.LocalTimeZone, options.Value.Hour);

    /// <summary>
    /// How long until the next <paramref name="hour"/>:00 on the local wall clock.
    /// </summary>
    // Each candidate run takes the offset in force *at that run*, not the offset now. Reusing
    // now's offset is right on every night but two a year: the evening before a DST change,
    // tomorrow's 04:00 was computed at tonight's offset and so landed at 03:00 or 05:00.
    //
    // Public and static, like PoliteRateLimitHandler.RetryDelayFor, so the rule is testable
    // against a real DST zone without driving the background loop.
    public static TimeSpan TimeUntilNextRun(DateTimeOffset utcNow, TimeZoneInfo localZone, int hour)
    {
        ArgumentNullException.ThrowIfNull(localZone);

        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, localZone).DateTime);

        var todaysRun = RunOn(localToday);
        if (todaysRun > utcNow)
        {
            return todaysRun - utcNow;
        }

        return RunOn(localToday.AddDays(1)) - utcNow;

        DateTimeOffset RunOn(DateOnly day)
        {
            var wallClock = day.ToDateTime(new TimeOnly(hour, 0));

            return new DateTimeOffset(wallClock, localZone.GetUtcOffset(wallClock));
        }
    }
}
