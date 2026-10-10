using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

public sealed class TrackedSeriesQueryServiceTests
{
    [Fact]
    public async Task GetTrackedIdsAsync_SeriesFromBothSources_ReturnsEachExternalId()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Series.Add(CreateSeries("Tanya", aniListId: 101, mangaUpdatesId: null));
        dbContext.Series.Add(CreateSeries("Slime", aniListId: null, mangaUpdatesId: 17992364412));
        dbContext.Series.Add(CreateSeries("Local", aniListId: null, mangaUpdatesId: null));
        await dbContext.SaveChangesAsync();

        var response = await new TrackedSeriesQueryService(dbContext)
            .GetTrackedIdsAsync(CancellationToken.None);

        response.AniListIds.ShouldBe([101]);
        response.MangaUpdatesIds.ShouldBe([17992364412]);
    }

    [Fact]
    public async Task GetTrackedIdsAsync_AnEmptyShelf_IsEmptyRatherThanNull()
    {
        await using var dbContext = CreateDbContext();

        var response = await new TrackedSeriesQueryService(dbContext)
            .GetTrackedIdsAsync(CancellationToken.None);

        response.AniListIds.ShouldBeEmpty();
        response.MangaUpdatesIds.ShouldBeEmpty();
    }

    // Another user's shelf must not leak into this user's "On shelf" markers.
    [Fact]
    public async Task GetTrackedIdsAsync_OnlyAnotherUsersSeries_IsEmpty()
    {
        var databaseName = $"tracked-{Guid.CreateVersion7()}";
        await using (var otherUser = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse)))
        {
            otherUser.Series.Add(CreateSeries("Theirs", aniListId: 202, mangaUpdatesId: 5));
            await otherUser.SaveChangesAsync();
        }

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);

        var response = await new TrackedSeriesQueryService(dbContext)
            .GetTrackedIdsAsync(CancellationToken.None);

        response.AniListIds.ShouldBeEmpty();
        response.MangaUpdatesIds.ShouldBeEmpty();
    }

    private static Series CreateSeries(string title, int? aniListId, long? mangaUpdatesId) => new()
    {
        Id = Guid.CreateVersion7(),
        TitleEnglish = title,
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        AniListId = aniListId,
        MangaUpdatesId = mangaUpdatesId,
        UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
    };

    private static LnShelfDbContext CreateDbContext() =>
        CreateDbContext($"tracked-{Guid.CreateVersion7()}", TestUsers.Current);

    private static LnShelfDbContext CreateDbContext(string databaseName, ICurrentUser currentUser) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, currentUser);
}
