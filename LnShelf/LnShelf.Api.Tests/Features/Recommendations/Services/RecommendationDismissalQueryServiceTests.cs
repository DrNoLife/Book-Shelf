using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

public sealed class RecommendationDismissalQueryServiceTests
{
    [Fact]
    public async Task ListAsync_TwoUsers_NewestFirstAndOnlyTheCallers()
    {
        var database = $"dismissals-{Guid.CreateVersion7()}";
        await SeedAsync(database, TestUsers.Owner, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(database, TestUsers.Owner, 2, new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(database, TestUsers.Owner, 3, new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(database, TestUsers.SomeoneElse, 4, new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));

        await using var dbContext = Context(database, TestUsers.Owner);
        var list = await new RecommendationDismissalQueryService(dbContext).ListAsync(CancellationToken.None);

        // Same instant: the lower catalogue id first, so the order never wobbles between calls.
        list.Select(dismissal => dismissal.AniListId).ShouldBe([2, 3, 1]);
        list[0].Title.ShouldBe("Work 2");
        list[0].Format.ShouldBe(Format.Manga);
    }

    private static async Task SeedAsync(string database, Guid userId, int aniListId, DateTimeOffset dismissedAt)
    {
        await using var dbContext = Context(database, userId);
        dbContext.RecommendationDismissals.Add(new RecommendationDismissal
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            Title = $"Work {aniListId}",
            Format = Format.Manga,
            DismissedAt = dismissedAt,
        });
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));
}
