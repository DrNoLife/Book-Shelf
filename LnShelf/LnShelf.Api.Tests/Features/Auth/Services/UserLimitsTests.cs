using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>A user's share of the server, counted on their own shelf only.</summary>
public sealed class UserLimitsTests
{
    [Fact]
    public async Task EnsureRoomForSeriesAsync_AtTheLimit_Refuses()
    {
        await using var dbContext = CreateDbContext(TestUsers.Owner);
        AddSeries(dbContext, count: 2);
        await dbContext.SaveChangesAsync();

        var act = () => Limits(dbContext, maximumSeries: 2).EnsureRoomForSeriesAsync(adding: 1, CancellationToken.None);

        (await act.ShouldThrowAsync<ConflictException>()).Message.ShouldContain("at most 2 series");
    }

    [Fact]
    public async Task EnsureRoomForSeriesAsync_AnotherUsersSeries_DoNotCount()
    {
        var database = $"user-limits-{Guid.CreateVersion7()}";
        await using (var someoneElse = CreateDbContext(TestUsers.SomeoneElse, database))
        {
            AddSeries(someoneElse, count: 2);
            await someoneElse.SaveChangesAsync();
        }

        await using var owner = CreateDbContext(TestUsers.Owner, database);

        await Limits(owner, maximumSeries: 2).EnsureRoomForSeriesAsync(adding: 1, CancellationToken.None);
    }

    private static void AddSeries(LnShelfDbContext dbContext, int count)
    {
        for (var index = 0; index < count; index++)
        {
            dbContext.Series.Add(new Series
            {
                Id = Guid.CreateVersion7(),
                TitleEnglish = $"Series {index}",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Ongoing,
            });
        }
    }

    private static UserLimits Limits(LnShelfDbContext dbContext, int maximumSeries) =>
        new(dbContext, Options.Create(new UserLimitOptions { MaximumSeriesPerUser = maximumSeries }));

    private static LnShelfDbContext CreateDbContext(Guid userId, string? database = null) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(database ?? $"user-limits-{Guid.CreateVersion7()}")
            .Options, TestUsers.As(userId));
}
