using LnShelf.Api.Tests.Features.Covers.Fixtures;
using LnShelf.Shared.Covers;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// What one user is offered from the covers others published: matched on the catalogue id,
/// format and number, never on anything else.
/// </summary>
public sealed class CommunityCoverQueryServiceTests
{
    private static readonly Guid Owner = TestUsers.Owner;
    private static readonly Guid Other = TestUsers.SomeoneElse;
    private static readonly Guid Third = CommunityCoverShelf.Third;

    private static readonly DateTimeOffset Early = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Late = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] FirstImage = [1, 1, 1];
    private static readonly byte[] SecondImage = [2, 2, 2];

    [Fact]
    public async Task ListForVolumeAsync_SameAniListId_OffersTheOtherUsersCover()
    {
        var (database, store, volumeId) = await ArrangeAsync();
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);

        var response = await ListAsync(database, store, volumeId);

        var candidate = response.Candidates.ShouldHaveSingleItem();
        candidate.ContentHash.ShouldBe(CoverContentHash.Compute(FirstImage));
        candidate.SharedBy.ShouldBe(["Alice"]);
        candidate.UsedByCount.ShouldBe(1);
        candidate.IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task ListForVolumeAsync_ASharerWhoseAddressIsOnlySeparators_IsNotShownByTheirEmail()
    {
        var database = CommunityCoverShelf.NewDatabase();
        await CommunityCoverShelf.AddUserAsync(database, Owner, "owner@example.com");
        await CommunityCoverShelf.AddUserAsync(database, Other, "._-@example.com");
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);
        var store = new InMemoryCoverStore();
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldHaveSingleItem().SharedBy.ShouldBe(["Someone"]);
    }

    [Fact]
    public async Task ListForVolumeAsync_SameMangaUpdatesIdOnly_OffersTheOtherUsersCover()
    {
        var (database, store, _) = await ArrangeAsync();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(
            database, Owner, number: 2, aniListId: null, mangaUpdatesId: 45348350266, format: Format.Manga);
        await CommunityCoverShelf.PublishAsync(
            database, Other, store, FirstImage, Early,
            aniListId: null, mangaUpdatesId: 45348350266, format: Format.Manga, number: 2);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(Format.Manga, 1, 94846)]
    [InlineData(Format.LightNovel, 2, 94846)]
    [InlineData(Format.LightNovel, 1, 11111)]
    public async Task ListForVolumeAsync_FormatNumberOrIdDiffers_OffersNothing(Format format, decimal number, int aniListId)
    {
        var (database, store, volumeId) = await ArrangeAsync();
        await CommunityCoverShelf.PublishAsync(
            database, Other, store, FirstImage, Early, aniListId: aniListId, format: format, number: number);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListForVolumeAsync_AManualSeriesWithNoCatalogueId_OffersNothing()
    {
        var (database, store, _) = await ArrangeAsync();
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner, aniListId: null);
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListForVolumeAsync_TheUsersOwnPublishedCover_IsNotOffered()
    {
        var (database, store, volumeId) = await ArrangeAsync();
        await CommunityCoverShelf.PublishAsync(database, Owner, store, FirstImage, Early);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListForVolumeAsync_AVolumeOfAnotherUser_IsNull()
    {
        var (database, store, _) = await ArrangeAsync();
        var othersVolumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Other);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var response = await CommunityCoverShelf.Queries(dbContext, Owner, store)
            .ListForVolumeAsync(othersVolumeId, CancellationToken.None);

        response.ShouldBeNull();
    }

    [Fact]
    public async Task ListForVolumeAsync_CoversWithDifferentSharerCounts_OrdersByDistinctSharersThenEarliest()
    {
        var (database, store, volumeId) = await ArrangeAsync();

        // One image shared by two users, published later than an image only one user has.
        await CommunityCoverShelf.PublishAsync(database, Third, store, SecondImage, Early);
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Late);
        await CommunityCoverShelf.PublishAsync(database, Third, store, FirstImage, Late.AddHours(1), number: 1);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.Select(candidate => candidate.ContentHash)
            .ShouldBe([CoverContentHash.Compute(FirstImage), CoverContentHash.Compute(SecondImage)]);
        response.Candidates[0].UsedByCount.ShouldBe(2);
        response.Candidates[0].SharedBy.ShouldBe(["Alice", "Bob"]);
    }

    [Fact]
    public async Task ListForVolumeAsync_TheImageIsGone_DropsThatCover()
    {
        var (database, store, volumeId) = await ArrangeAsync();
        var row = await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);
        await store.DeleteAsync(row.ObjectKey, CancellationToken.None);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListForVolumeAsync_AnotherSharersCopyOfTheImageSurvives_StillOffersIt()
    {
        var (database, store, volumeId) = await ArrangeAsync();
        var gone = await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);
        await CommunityCoverShelf.PublishAsync(database, Third, store, FirstImage, Late);
        await store.DeleteAsync(gone.ObjectKey, CancellationToken.None);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ListForVolumeAsync_TheUsersOwnCoverHasTheSameBytes_IsCurrent()
    {
        var (database, store, _) = await ArrangeAsync();
        store.Put("override/volume/mine/cover.webp", FirstImage);
        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(
            database, Owner, number: 3, overrideObjectKey: "override/volume/mine/cover.webp");
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early, number: 3);
        await CommunityCoverShelf.PublishAsync(database, Third, store, SecondImage, Early, number: 3);

        var response = await ListAsync(database, store, volumeId);

        response.Candidates.Single(candidate => candidate.ContentHash == CoverContentHash.Compute(FirstImage))
            .IsCurrent.ShouldBeTrue();
        response.Candidates.Single(candidate => candidate.ContentHash == CoverContentHash.Compute(SecondImage))
            .IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task ListForVolumeAsync_AHiddenCover_IsLeftOutForTheHiderOnly()
    {
        var (database, store, volumeId) = await ArrangeAsync();
        var otherVolumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Third);
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);

        await using (var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner))
        {
            await CommunityCoverShelf.Commands(dbContext, Owner, store)
                .HideAsync(CoverContentHash.Compute(FirstImage), CancellationToken.None);
        }

        (await ListAsync(database, store, volumeId)).Candidates.ShouldBeEmpty();

        // Hiding is the hider's own: another user tracking the same volume still sees it.
        await using var thirdContext = CommunityCoverShelf.CreateDbContext(database, Third);
        var forThird = await CommunityCoverShelf.Queries(thirdContext, Third, store)
            .ListForVolumeAsync(otherVolumeId, CancellationToken.None);
        forThird.ShouldNotBeNull().Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ReadImageAsync_APublishedHash_ReturnsTheBytesAndAnUnknownOneNull()
    {
        var (database, store, _) = await ArrangeAsync();
        await CommunityCoverShelf.PublishAsync(database, Other, store, FirstImage, Early);

        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        var queries = CommunityCoverShelf.Queries(dbContext, Owner, store);

        var content = await queries.ReadImageAsync(CoverContentHash.Compute(FirstImage), CancellationToken.None);
        content.ShouldNotBeNull().Content.ShouldBe(FirstImage);

        (await queries.ReadImageAsync(CoverContentHash.Compute(SecondImage), CancellationToken.None)).ShouldBeNull();
    }

    private static async Task<CommunityCoversResponse> ListAsync(
        string database, InMemoryCoverStore store, Guid volumeId)
    {
        await using var dbContext = CommunityCoverShelf.CreateDbContext(database, Owner);
        return (await CommunityCoverShelf.Queries(dbContext, Owner, store)
            .ListForVolumeAsync(volumeId, CancellationToken.None)).ShouldNotBeNull();
    }

    /// <summary>The owner has volume 1 of the light novel; Alice (Other) and Bob (Third) exist as users.</summary>
    private static async Task<(string Database, InMemoryCoverStore Store, Guid VolumeId)> ArrangeAsync()
    {
        var database = CommunityCoverShelf.NewDatabase();
        await CommunityCoverShelf.AddUserAsync(database, Owner, "owner@example.com");
        await CommunityCoverShelf.AddUserAsync(database, Other, "alice@example.com");
        await CommunityCoverShelf.AddUserAsync(database, Third, "bob@example.com");

        var volumeId = await CommunityCoverShelf.SeedVolumeAsync(database, Owner);
        return (database, new InMemoryCoverStore(), volumeId);
    }
}
