using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// The owner filters on LnShelfDbContext: one user's shelf is invisible to every other user,
/// and work with no user sees nothing and cannot write (feature-spec §7).
/// </summary>
public sealed class OwnerIsolationTests
{
    [Fact]
    public async Task Queries_AnotherUsersShelf_SeesNothingOfIt()
    {
        var database = $"owner-isolation-{Guid.CreateVersion7()}";
        await SeedSeriesAsync(database, TestUsers.Owner);

        await using var someoneElse = Context(database, TestUsers.SomeoneElse);

        (await someoneElse.Series.CountAsync()).ShouldBe(0);
        (await someoneElse.Volumes.CountAsync()).ShouldBe(0);
        (await someoneElse.SeriesUserStates.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Queries_TheOwnersShelf_SeesAllOfIt()
    {
        var database = $"owner-isolation-{Guid.CreateVersion7()}";
        await SeedSeriesAsync(database, TestUsers.Owner);

        await using var owner = Context(database, TestUsers.Owner);

        (await owner.Series.CountAsync()).ShouldBe(1);
        (await owner.Volumes.CountAsync()).ShouldBe(2);
    }

    // Fails closed: background work that forgot ActAs reads an empty shelf, not everyone's.
    [Fact]
    public async Task Queries_NoCurrentUser_SeesNothing()
    {
        var database = $"owner-isolation-{Guid.CreateVersion7()}";
        await SeedSeriesAsync(database, TestUsers.Owner);

        await using var nobody = Context(database, userId: null);

        (await nobody.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SaveChanges_NewSeries_IsStampedWithTheCurrentUser()
    {
        var database = $"owner-isolation-{Guid.CreateVersion7()}";
        await SeedSeriesAsync(database, TestUsers.SomeoneElse);

        await using var check = Context(database, TestUsers.SomeoneElse);
        (await check.Series.SingleAsync()).OwnerId.ShouldBe(TestUsers.SomeoneElse);
    }

    [Fact]
    public async Task SaveChanges_NewSeriesWithNoCurrentUser_Throws()
    {
        await using var nobody = Context($"owner-isolation-{Guid.CreateVersion7()}", userId: null);
        nobody.Series.Add(NewSeries());

        await Should.ThrowAsync<InvalidOperationException>(() => nobody.SaveChangesAsync());
    }

    private static async Task SeedSeriesAsync(string database, Guid ownerId)
    {
        await using var context = Context(database, ownerId);
        context.Series.Add(NewSeries());
        await context.SaveChangesAsync();
    }

    private static Series NewSeries()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85470,
            TitleEnglish = "Mushoku Tensei",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 1 });
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 2 });
        return series;
    }

    private static LnShelfDbContext Context(string database, Guid? userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));
}
