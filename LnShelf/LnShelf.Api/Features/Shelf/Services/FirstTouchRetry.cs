namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Saves a write that may be the first touch of a volume, retrying it once when a concurrent
/// request created the same user-state row first.
/// </summary>
// A user-state row exists only once something was recorded, so two writes on an untouched
// volume (a double-click, a second tab) both insert it and the primary key refuses the
// later one. The loser's intent is still valid on top of the winner's row, so the whole
// load-and-apply runs once more against a fresh load instead of answering 409 and dropping
// the click. A second refusal is not this race and goes to the central handler (409).
public static class FirstTouchRetry
{
    /// <param name="loadAndApply">
    /// Loads the series and applies the change to it, untouched by any save. Null when there
    /// is nothing to write, which is answered as null without saving.
    /// </param>
    /// <returns>The series as saved, or null when <paramref name="loadAndApply"/> found nothing.</returns>
    public static async Task<Series?> SaveAsync(
        LnShelfDbContext dbContext,
        Func<CancellationToken, Task<Series?>> loadAndApply,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(loadAndApply);
        ArgumentNullException.ThrowIfNull(logger);

        var series = await loadAndApply(cancellationToken);
        if (series is null)
        {
            return null;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return series;
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation)
        {
            logger.LogInformation(
                "A write on {SeriesId} lost a first-touch race; applying it again on top of the row that won",
                series.Id);

            // Everything tracked in this scope is this write's own, and the failed inserts would
            // be sent again by the next save if they stayed tracked.
            dbContext.ChangeTracker.Clear();
        }

        series = await loadAndApply(cancellationToken);
        if (series is null)
        {
            return null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return series;
    }
}
