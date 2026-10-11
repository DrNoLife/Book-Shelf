using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Api.Features.Calendar.Mappers;

namespace LnShelf.Api.Features.Calendar.Services;

/// <inheritdoc />
// The second request path that reads a shelf for someone without a session, and it does so
// the way SharedShelfQueryService does: it finds the owner from the token, then reads in a
// scope of its own that acts as that owner, so the ordinary owner filters do the isolating.
// The request's own scope stays anonymous.
public sealed class ReleaseCalendarQueryService(
    ICalendarFeedResolver resolver,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IReleaseCalendarQueryService
{
    /// <summary>How far back a dated volume stays on the calendar.</summary>
    // Long enough that last month's releases are still there to look back at, short enough
    // that a long-running series does not fill years of a calendar with history.
    public const int PastDays = 60;

    public async Task<string?> GetAsync(string token, CancellationToken cancellationToken)
    {
        if (await resolver.FindOwnerAsync(token, cancellationToken) is not { } ownerId)
        {
            return null;
        }

        var from = timeProvider.Today().AddDays(-PastDays);

        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().ActAs(ownerId);
        var ownerContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();

        // Every tracked series, owned or not, as Upcoming reads it: a dropped series' dates
        // are noise, and a series with no user-state row has not been dropped.
        var entries = await ownerContext.Volumes
            .AsNoTracking()
            .Where(volume => volume.ReleaseDate != null && volume.ReleaseDate >= from)
            .Where(volume => volume.Series!.UserState == null
                             || volume.Series.UserState.TrackingStatus != TrackingStatus.Dropped)
            .Select(volume => new CalendarEntry(volume.Id, volume.Series!.TitleEnglish, volume.Series.Format, volume.Number, volume.ReleaseDate!.Value))
            .ToListAsync(cancellationToken);

        // Ordered in memory: a calendar app does not care, but a stable order makes the
        // document diffable and the tests readable.
        var ordered = entries
            .OrderBy(entry => entry.ReleaseDate)
            .ThenBy(entry => entry.SeriesTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.VolumeNumber);

        return IcsCalendarWriter.Write(ordered, timeProvider.GetUtcNow());
    }
}
