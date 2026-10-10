namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The nightly refresh: a <c>BackgroundService</c>, not a job framework
/// (CLAUDE.md — one nightly loop does not justify Hangfire).
/// </summary>
public sealed class NightlySyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    IOptions<OpenLibraryOptions> lookupOptions,
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

    // One run per user, each in its own scope acting as that user, so every user gets their
    // own run, discrepancies and sidebar summary, and the owner filters keep each run to that
    // user's series (feature-spec §7). The lookup allowance is the night's, shared by all.
    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        var budget = new LookupBudget(lookupOptions.Value.MaximumLookupsPerNight);

        foreach (var userId in await UserIdsAsync(stoppingToken))
        {
            await RunForUserAsync(userId, budget, stoppingToken);
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

    private async Task RunForUserAsync(Guid userId, LookupBudget budget, CancellationToken stoppingToken)
    {
        // A scope per run: the sync service is scoped because the DbContext is.
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(userId);
        var sync = scope.ServiceProvider.GetRequiredService<ISeriesSyncService>();

        try
        {
            await sync.RunAsync(budget, stoppingToken);
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
