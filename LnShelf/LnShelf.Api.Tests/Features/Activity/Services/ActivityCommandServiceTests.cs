using LnShelf.Api.Features.Activity.Services;
using LnShelf.Api.Features.Preferences.Models;
using LnShelf.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Activity.Fixtures.ActivityShelf;

namespace LnShelf.Api.Tests.Features.Activity.Services;

/// <summary>
/// "Got it" moves the line the panel reads from, and nothing else on the preferences row.
/// </summary>
public sealed class ActivityCommandServiceTests
{
    [Fact]
    public async Task MarkSeenAsync_NoPreferencesRow_CreatesOneSeenNow()
    {
        await using var dbContext = CreateDbContext();

        await CreateService(dbContext).MarkSeenAsync(through: null, CancellationToken.None);

        (await dbContext.UserPreferences.SingleAsync()).ActivitySeenAt.ShouldBe(Now);
    }

    // A sync wrote news after the panel loaded: "Got it" acknowledges what was shown only.
    [Fact]
    public async Task MarkSeenAsync_EventNewerThanTheCursor_StillShowsAfterwards()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = AddSeries(dbContext, "Overlord");
        AddEvent(dbContext, seriesId, 17, Now.AddHours(-2));
        await dbContext.SaveChangesAsync();
        var shown = await new ActivityQueryService(dbContext, FixedClock()).GetActivityAsync(CancellationToken.None);
        AddEvent(dbContext, seriesId, 18, Now.AddHours(-1));
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).MarkSeenAsync(shown.Through, CancellationToken.None);
        var activity = await new ActivityQueryService(dbContext, FixedClock()).GetActivityAsync(CancellationToken.None);

        activity.Series.ShouldHaveSingleItem().Events.ShouldHaveSingleItem().VolumeNumber.ShouldBe(18m);
    }

    [Theory]
    [InlineData(1, 0)]     // a cursor in the future is capped at now
    [InlineData(-3, -1)]   // never moves back past what was already seen
    public async Task MarkSeenAsync_CursorOutOfBounds_IsClamped(int cursorHours, int expectedHours)
    {
        await using var dbContext = CreateDbContext();
        dbContext.UserPreferences.Add(new UserPreferences { ActivitySeenAt = Now.AddHours(-1) });
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).MarkSeenAsync(Now.AddHours(cursorHours), CancellationToken.None);

        (await dbContext.UserPreferences.SingleAsync()).ActivitySeenAt.ShouldBe(Now.AddHours(expectedHours));
    }

    [Fact]
    public async Task MarkSeenAsync_ThenRead_ShowsNothingAndKeepsTheOtherSettings()
    {
        await using var dbContext = CreateDbContext();
        var seriesId = AddSeries(dbContext, "Overlord");
        dbContext.UserPreferences.Add(new UserPreferences { GridDensity = GridDensity.Compact });
        AddEvent(dbContext, seriesId, 17, Now.AddHours(-1));
        await dbContext.SaveChangesAsync();

        await CreateService(dbContext).MarkSeenAsync(through: null, CancellationToken.None);
        var activity = await new ActivityQueryService(dbContext, FixedClock()).GetActivityAsync(CancellationToken.None);

        activity.Series.ShouldBeEmpty();
        (await dbContext.UserPreferences.SingleAsync()).GridDensity.ShouldBe(GridDensity.Compact);
    }

    private static ActivityCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, FixedClock(), NullLogger<ActivityCommandService>.Instance);
}
