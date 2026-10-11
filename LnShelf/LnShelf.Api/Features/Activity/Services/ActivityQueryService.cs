using LnShelf.Api.Features.Activity.Abstractions;
using LnShelf.Api.Features.Activity.Mappers;
using LnShelf.Shared.Activity;

namespace LnShelf.Api.Features.Activity.Services;

/// <inheritdoc />
public sealed class ActivityQueryService(LnShelfDbContext dbContext, TimeProvider timeProvider)
    : IActivityQueryService
{
    /// <summary>How far back the panel looks for a reader who has never dismissed it.</summary>
    // Two weeks rather than the whole 120-day log: a new reader's first visit should say
    // what is new, not replay four months of history in one panel.
    public static readonly TimeSpan FirstVisitWindow = TimeSpan.FromDays(14);

    public async Task<ActivityResponse> GetActivityAsync(CancellationToken cancellationToken)
    {
        var seenAt = await dbContext.UserPreferences
            .AsNoTracking()
            .Select(preferences => preferences.ActivitySeenAt)
            .FirstOrDefaultAsync(cancellationToken);

        var since = seenAt ?? timeProvider.GetUtcNow() - FirstVisitWindow;

        // Every event in the window, not just the newest thirty: the grouping keeps one line
        // per volume, and a cut taken before it could drop a volume's newest line while
        // keeping an older one. The window is bounded by the 120-day prune.
        var entries = await dbContext.ShelfEvents
            .AsNoTracking()
            .Where(shelfEvent => shelfEvent.OccurredAt > since)
            .Select(shelfEvent => new ActivityEntry(
                shelfEvent.SeriesId,
                shelfEvent.Series!.TitleEnglish,
                shelfEvent.Series.Format,
                shelfEvent.Kind,
                shelfEvent.VolumeNumber,
                shelfEvent.PreviousDate,
                shelfEvent.NewDate,
                shelfEvent.OccurredAt))
            .ToListAsync(cancellationToken);

        return ActivityGrouping.Group(entries);
    }
}
