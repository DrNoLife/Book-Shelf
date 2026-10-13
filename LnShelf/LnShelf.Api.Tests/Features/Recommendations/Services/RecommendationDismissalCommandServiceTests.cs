using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;
using LnShelf.Shared.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

/// <summary>
/// "Not interested": a dismissal is stored once per owner, validated at the door, capped,
/// and never visible to another user.
/// </summary>
public sealed class RecommendationDismissalCommandServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DismissAsync_ANewWork_StoresTitleFormatAndTime()
    {
        var database = NewDatabaseName();
        await using var dbContext = Context(database, TestUsers.Owner);

        await Service(dbContext, TestUsers.Owner, Now).DismissAsync(85737, Request("  Overlord  "), CancellationToken.None);

        var stored = (await dbContext.RecommendationDismissals.ToListAsync()).ShouldHaveSingleItem();
        stored.AniListId.ShouldBe(85737);
        stored.Title.ShouldBe("Overlord");
        stored.Format.ShouldBe(Format.LightNovel);
        stored.DismissedAt.ShouldBe(Now);
        stored.OwnerId.ShouldBe(TestUsers.Owner);
    }

    [Fact]
    public async Task DismissAsync_Twice_KeepsOneRowAndTheFirstTime()
    {
        var database = NewDatabaseName();
        await using var dbContext = Context(database, TestUsers.Owner);

        await Service(dbContext, TestUsers.Owner, Now).DismissAsync(85737, Request("Overlord"), CancellationToken.None);
        await Service(dbContext, TestUsers.Owner, Now.AddDays(1)).DismissAsync(85737, Request("Overlord"), CancellationToken.None);

        var stored = (await dbContext.RecommendationDismissals.ToListAsync()).ShouldHaveSingleItem();
        stored.DismissedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(0, "Overlord", Format.LightNovel)]
    [InlineData(-1, "Overlord", Format.LightNovel)]
    [InlineData(85737, "   ", Format.LightNovel)]
    [InlineData(85737, null, Format.LightNovel)]
    [InlineData(85737, "Overlord", (Format)99)]
    public async Task DismissAsync_InvalidInput_IsRefused(int aniListId, string? title, Format format)
    {
        await using var dbContext = Context(NewDatabaseName(), TestUsers.Owner);

        var act = () => Service(dbContext, TestUsers.Owner, Now).DismissAsync(
            aniListId,
            new DismissRecommendationRequest { Title = title!, Format = format },
            CancellationToken.None);

        await Should.ThrowAsync<InvalidInputException>(act);
        (await dbContext.RecommendationDismissals.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DismissAsync_ATitleOver400Characters_IsRefused()
    {
        await using var dbContext = Context(NewDatabaseName(), TestUsers.Owner);

        var act = () => Service(dbContext, TestUsers.Owner, Now)
            .DismissAsync(85737, Request(new string('a', RecommendationDismissal.TitleMaximumLength + 1)), CancellationToken.None);

        await Should.ThrowAsync<InvalidInputException>(act);
    }

    [Fact]
    public async Task DismissAsync_AtTheCap_IsConflict()
    {
        var database = NewDatabaseName();
        await using var dbContext = Context(database, TestUsers.Owner);
        for (var id = 1; id <= RecommendationDismissalCommandService.MaximumDismissalsPerUser; id++)
        {
            dbContext.RecommendationDismissals.Add(NewDismissal(id));
        }

        await dbContext.SaveChangesAsync();

        var act = () => Service(dbContext, TestUsers.Owner, Now)
            .DismissAsync(999_999, Request("One too many"), CancellationToken.None);

        await Should.ThrowAsync<ConflictException>(act);
    }

    [Fact]
    public async Task DismissAsync_SameWorkAsAnotherUser_EachHasTheirOwn()
    {
        var database = NewDatabaseName();
        await using var ownerContext = Context(database, TestUsers.Owner);
        await using var otherContext = Context(database, TestUsers.SomeoneElse);

        await Service(ownerContext, TestUsers.Owner, Now).DismissAsync(85737, Request("Overlord"), CancellationToken.None);
        await Service(otherContext, TestUsers.SomeoneElse, Now).DismissAsync(85737, Request("Overlord"), CancellationToken.None);

        (await ownerContext.RecommendationDismissals.ToListAsync()).ShouldHaveSingleItem().OwnerId.ShouldBe(TestUsers.Owner);
        (await otherContext.RecommendationDismissals.ToListAsync()).ShouldHaveSingleItem().OwnerId.ShouldBe(TestUsers.SomeoneElse);
    }

    [Fact]
    public async Task RestoreAsync_AStoredDismissal_RemovesItAndIsTrue()
    {
        var database = NewDatabaseName();
        await using var dbContext = Context(database, TestUsers.Owner);
        await Service(dbContext, TestUsers.Owner, Now).DismissAsync(85737, Request("Overlord"), CancellationToken.None);

        var restored = await Service(dbContext, TestUsers.Owner, Now).RestoreAsync(85737, CancellationToken.None);

        restored.ShouldBeTrue();
        (await dbContext.RecommendationDismissals.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RestoreAsync_NothingStored_IsFalse()
    {
        await using var dbContext = Context(NewDatabaseName(), TestUsers.Owner);

        var restored = await Service(dbContext, TestUsers.Owner, Now).RestoreAsync(85737, CancellationToken.None);

        restored.ShouldBeFalse();
    }

    [Fact]
    public async Task RestoreAsync_AnotherUsersDismissal_IsFalseAndTheirsStays()
    {
        var database = NewDatabaseName();
        await using var ownerContext = Context(database, TestUsers.Owner);
        await using var otherContext = Context(database, TestUsers.SomeoneElse);
        await Service(otherContext, TestUsers.SomeoneElse, Now).DismissAsync(85737, Request("Overlord"), CancellationToken.None);

        var restored = await Service(ownerContext, TestUsers.Owner, Now).RestoreAsync(85737, CancellationToken.None);

        restored.ShouldBeFalse();
        (await otherContext.RecommendationDismissals.CountAsync()).ShouldBe(1);
    }

    private static DismissRecommendationRequest Request(string title) =>
        new() { Title = title, Format = Format.LightNovel };

    private static RecommendationDismissal NewDismissal(int aniListId) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = aniListId,
        Title = $"Work {aniListId}",
        Format = Format.Manga,
        DismissedAt = Now,
    };

    private static RecommendationDismissalCommandService Service(LnShelfDbContext dbContext, Guid userId, DateTimeOffset now) =>
        new(dbContext, TestUsers.As(userId), new FixedTimeProvider(now), NullLogger<RecommendationDismissalCommandService>.Instance);

    private static string NewDatabaseName() => $"dismissals-{Guid.CreateVersion7()}";

    private static LnShelfDbContext Context(string database, Guid userId) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase(database).Options, TestUsers.As(userId));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
