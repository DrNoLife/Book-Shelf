namespace LnShelf.Api.Features.Notifications.Services;

/// <inheritdoc />
// Runs after a user's run, inside the run's lease and ActAs scope, never in the middle of it:
// one digest per run rather than one push per series (D4), and a push service being down must
// not cost anyone their sync. Hence best effort, and its own try/catch.
//
// shelf_event.NotifiedAt is the outbox. Every event this pass looks at is marked — delivered,
// muted, stale, or with nowhere to go — except when nothing got through and a device might
// still take it (NotificationDelivery.ShouldRetry). The seven-day cutoff is what bounds those
// retries.
public sealed class ReleaseNotificationDispatcher(
    LnShelfDbContext dbContext,
    IReleaseNotifier notifier,
    TimeProvider timeProvider,
    ILogger<ReleaseNotificationDispatcher> logger) : IReleaseNotificationDispatcher
{
    /// <summary>Older than this, an event is only shown in the app, never pushed.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    /// <summary>The activity log's retention (D3).</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(120);

    // Two best-effort steps, each with its own catch: the retention the privacy page promises
    // must not depend on the push path working. A send that throws on every pass would
    // otherwise keep that user's activity log forever.
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SendPendingAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
                                          || !cancellationToken.IsCancellationRequested)
        {
            // Swallowed on purpose: the sync this follows has already succeeded or failed on
            // its own, and the events are still pending for the next pass. Whatever the send
            // half-marked is dropped, so the prune's save below cannot write it.
            dbContext.ChangeTracker.DiscardPendingChanges();
            logger.LogWarning(exception, "The notification pass failed; the events stay pending for the next one");
        }

        try
        {
            await PruneAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
                                          || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Pruning the activity log failed; the next pass tries again");
        }
    }

    private async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await dbContext.ShelfEvents
            .Include(shelfEvent => shelfEvent.Series)
            .ThenInclude(series => series!.UserState)
            .Where(shelfEvent => shelfEvent.NotifiedAt == null)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var staleBefore = now - StaleAfter;

        // Muted series still record their events, for "Since your last visit"; mute is about
        // pushes only (D6).
        var toSend = pending
            .Where(shelfEvent => shelfEvent.OccurredAt >= staleBefore
                                 && shelfEvent.Series?.UserState?.NotificationsMuted is not true)
            .ToList();

        var delivery = toSend.Count > 0 && notifier.IsAvailable
            ? await notifier.NotifyAsync(ReleaseDigestMapper.ToNotification(toSend), cancellationToken)
            : NotificationDelivery.Nowhere;

        var keepPending = delivery.ShouldRetry ? toSend.ToHashSet() : [];
        foreach (var shelfEvent in pending.Where(shelfEvent => !keepPending.Contains(shelfEvent)))
        {
            shelfEvent.NotifiedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Notification pass: {EventCount} events, {SentCount} sent to {Delivered} of {DeviceCount} devices, {SkippedCount} muted or stale{Retry}",
            pending.Count,
            toSend.Count,
            delivery.Delivered,
            delivery.Devices,
            pending.Count - toSend.Count,
            delivery.ShouldRetry ? "; will retry" : string.Empty);
    }

    // Loaded and removed rather than ExecuteDelete: the rows are few (one user's 120 days),
    // and this keeps the prune on the same owner-filtered, tracked path the tests can run.
    private async Task PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - KeepFor;

        var expired = await dbContext.ShelfEvents
            .Where(shelfEvent => shelfEvent.OccurredAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        dbContext.ShelfEvents.RemoveRange(expired);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Pruned {EventCount} activity entries older than {RetentionDays} days",
            expired.Count, (int)KeepFor.TotalDays);
    }
}
