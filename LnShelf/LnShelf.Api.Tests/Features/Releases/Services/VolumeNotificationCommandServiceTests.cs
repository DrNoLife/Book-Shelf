using LnShelf.Api.Features.Releases.Services;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Releases.Services;

/// <summary>"Notify me when dated": a stored one-shot request on one undated volume.</summary>
public sealed class VolumeNotificationCommandServiceTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    [Fact]
    public async Task SetNotifyWhenDatedAsync_AnUndatedVolume_CreatesTheRowWithTheFlag()
    {
        var database = NewDatabase();
        var volume = await AddVolumeAsync(database, TestUsers.Owner, new Volume { Number = 14 });
        await using var dbContext = CreateDbContext(database, TestUsers.Owner);

        (await CreateService(dbContext).SetNotifyWhenDatedAsync(volume.Id, true, CancellationToken.None)).ShouldBeTrue();

        (await dbContext.VolumeUserStates.SingleAsync()).NotifyWhenDated.ShouldBeTrue();
    }

    [Fact]
    public async Task SetNotifyWhenDatedAsync_OffWithNoRow_IsANoOp()
    {
        var database = NewDatabase();
        var volume = await AddVolumeAsync(database, TestUsers.Owner, new Volume { Number = 14 });
        await using var dbContext = CreateDbContext(database, TestUsers.Owner);

        (await CreateService(dbContext).SetNotifyWhenDatedAsync(volume.Id, false, CancellationToken.None)).ShouldBeTrue();

        (await dbContext.VolumeUserStates.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task SetNotifyWhenDatedAsync_AVolumeThatAlreadyHasADate_IsAConflict()
    {
        var database = NewDatabase();
        var volume = await AddVolumeAsync(database, TestUsers.Owner, new Volume { Number = 14, ReleaseDate = Today.AddMonths(2) });
        await using var dbContext = CreateDbContext(database, TestUsers.Owner);

        await Should.ThrowAsync<ConflictException>(
            () => CreateService(dbContext).SetNotifyWhenDatedAsync(volume.Id, true, CancellationToken.None));
    }

    [Fact]
    public async Task SetNotifyWhenDatedAsync_AVolumeThatIsOut_IsAConflict()
    {
        var database = NewDatabase();
        var volume = await AddVolumeAsync(database, TestUsers.Owner, new Volume { Number = 14, IsReleased = true });
        await using var dbContext = CreateDbContext(database, TestUsers.Owner);

        await Should.ThrowAsync<ConflictException>(
            () => CreateService(dbContext).SetNotifyWhenDatedAsync(volume.Id, true, CancellationToken.None));
    }

    [Fact]
    public async Task SetNotifyWhenDatedAsync_NoSuchVolume_IsFalse()
    {
        await using var dbContext = CreateDbContext(NewDatabase(), TestUsers.Owner);

        (await CreateService(dbContext).SetNotifyWhenDatedAsync(Guid.CreateVersion7(), true, CancellationToken.None))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task SetNotifyWhenDatedAsync_AnotherUsersVolume_IsFalse()
    {
        var database = NewDatabase();
        var volume = await AddVolumeAsync(database, TestUsers.SomeoneElse, new Volume { Number = 14 });
        await using var dbContext = CreateDbContext(database, TestUsers.Owner);

        (await CreateService(dbContext).SetNotifyWhenDatedAsync(volume.Id, true, CancellationToken.None)).ShouldBeFalse();
    }

    private static string NewDatabase() => $"volume-notifications-{Guid.CreateVersion7()}";

    private static async Task<Volume> AddVolumeAsync(string database, Guid userId, Volume volume)
    {
        var series = new Series { Id = Guid.CreateVersion7(), TitleEnglish = "86", Volumes = [volume] };
        volume.Id = Guid.CreateVersion7();
        volume.SeriesId = series.Id;

        await using var dbContext = CreateDbContext(database, userId);
        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();

        return volume;
    }

    private static VolumeNotificationCommandService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, new FixedTimeProvider(Today), NullLogger<VolumeNotificationCommandService>.Instance);

    private static LnShelfDbContext CreateDbContext(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    private sealed class FixedTimeProvider(DateOnly today) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
    }
}
