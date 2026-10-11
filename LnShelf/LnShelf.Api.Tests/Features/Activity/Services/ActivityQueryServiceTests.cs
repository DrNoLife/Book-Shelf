using LnShelf.Api.Features.Activity.Services;
using LnShelf.Api.Features.Preferences.Models;
using static LnShelf.Api.Tests.Features.Activity.Fixtures.ActivityShelf;

namespace LnShelf.Api.Tests.Features.Activity.Services;

/// <summary>
/// Which events "Since your last visit" reads.
/// </summary>
public sealed class ActivityQueryServiceTests
{
    // Never dismissed: a fixed window back, not the whole 120-day log.
    [Fact]
    public async Task GetActivityAsync_NeverSeen_ReadsTheFirstVisitWindow()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = AddSeries(dbContext, "Overlord");
        AddEvent(dbContext, seriesId, 17, Now - ActivityQueryService.FirstVisitWindow + TimeSpan.FromHours(1));
        AddEvent(dbContext, seriesId, 16, Now - ActivityQueryService.FirstVisitWindow - TimeSpan.FromHours(1));
        await dbContext.SaveChangesAsync();

        var activity = await new ActivityQueryService(dbContext, FixedClock()).GetActivityAsync(CancellationToken.None);

        var series = activity.Series.ShouldHaveSingleItem();
        series.SeriesTitle.ShouldBe("Overlord");
        series.Events.ShouldHaveSingleItem().VolumeNumber.ShouldBe(17m);
    }

    [Fact]
    public async Task GetActivityAsync_SeenBefore_ReadsOnlyWhatCameAfter()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = AddSeries(dbContext, "Overlord");
        dbContext.UserPreferences.Add(new UserPreferences { ActivitySeenAt = Now.AddDays(-1) });
        AddEvent(dbContext, seriesId, 17, Now.AddHours(-2));
        AddEvent(dbContext, seriesId, 16, Now.AddDays(-2));
        await dbContext.SaveChangesAsync();

        var activity = await new ActivityQueryService(dbContext, FixedClock()).GetActivityAsync(CancellationToken.None);

        activity.Series.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().VolumeNumber.ShouldBe(17m);
    }
}
