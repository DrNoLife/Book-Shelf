using LnShelf.Api.Features.Covers.Services;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Auth.Services;

/// <summary>
/// The avatar's writes: when sign-in copies the provider's picture (and when it must not), and
/// the upload that overrides it.
/// </summary>
public sealed class AvatarCommandServiceTests
{
    private const string PictureUrl = "https://lh3.googleusercontent.com/a/picture=s96-c";

    [Fact]
    public async Task RefreshProviderCopyAsync_ANewPicture_IsCopiedAndThePreviousCopyDeleted()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: "https://example.com/old.jpg", providerKey: "avatar/old");
        var (service, coverStore, fetcher) = CreateService(dbContext);

        await service.RefreshProviderCopyAsync(user, PictureUrl, CancellationToken.None);

        await fetcher.Received(1).FetchAsync(new Uri(PictureUrl), Arg.Any<CancellationToken>());
        var saved = await dbContext.Users.AsNoTracking().SingleAsync();
        saved.ProviderPictureUrl.ShouldBe(PictureUrl);
        var objectKey = saved.ProviderAvatarObjectKey.ShouldNotBeNull();
        CoverObjectKey.AvatarUserIdOf(objectKey).ShouldBe(user.Id);
        await coverStore.Received(1).SaveAsync(objectKey, Arg.Any<ReadOnlyMemory<byte>>(), "image/webp", Arg.Any<CancellationToken>());
        await coverStore.Received(1).DeleteAsync("avatar/old", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshProviderCopyAsync_TheSamePictureWithItsCopyStored_FetchesNothing()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: PictureUrl, providerKey: "avatar/current");
        var (service, coverStore, fetcher) = CreateService(dbContext);
        coverStore.ExistsAsync("avatar/current", Arg.Any<CancellationToken>()).Returns(true);

        await service.RefreshProviderCopyAsync(user, PictureUrl, CancellationToken.None);

        await fetcher.DidNotReceiveWithAnyArgs().FetchAsync(default!, default);
        (await dbContext.Users.AsNoTracking().SingleAsync()).ProviderAvatarObjectKey.ShouldBe("avatar/current");
    }

    // A restore without the covers, or a sweep that raced a sign-in, leaves the row naming a
    // copy that is not there; the next sign-in puts it back.
    [Fact]
    public async Task RefreshProviderCopyAsync_TheSamePictureWithItsCopyGone_FetchesItAgain()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: PictureUrl, providerKey: "avatar/missing");
        var (service, _, fetcher) = CreateService(dbContext);

        await service.RefreshProviderCopyAsync(user, PictureUrl, CancellationToken.None);

        await fetcher.Received(1).FetchAsync(new Uri(PictureUrl), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public async Task RefreshProviderCopyAsync_NoUsablePictureClaim_KeepsTheCopyThereIs(string? pictureUrl)
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: PictureUrl, providerKey: "avatar/current");
        var (service, coverStore, fetcher) = CreateService(dbContext);

        await service.RefreshProviderCopyAsync(user, pictureUrl, CancellationToken.None);

        await fetcher.DidNotReceiveWithAnyArgs().FetchAsync(default!, default);
        await coverStore.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
        (await dbContext.Users.AsNoTracking().SingleAsync()).ProviderAvatarObjectKey.ShouldBe("avatar/current");
    }

    // A picture is decoration: whatever goes wrong fetching it, the sign-in carries on.
    [Fact]
    public async Task RefreshProviderCopyAsync_TheFetchFails_DoesNotThrowAndChangesNothing()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: null, providerKey: null);
        var (service, coverStore, fetcher) = CreateService(dbContext);
        fetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns<byte[]?>(_ => throw new HttpRequestException("Google is down"));

        await Should.NotThrowAsync(() => service.RefreshProviderCopyAsync(user, PictureUrl, CancellationToken.None));

        await coverStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default!, default);
        var saved = await dbContext.Users.AsNoTracking().SingleAsync();
        saved.ProviderPictureUrl.ShouldBeNull();
        saved.ProviderAvatarObjectKey.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshProviderCopyAsync_TheSourceSendsNoUsableImage_ChangesNothing()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext, pictureUrl: null, providerKey: null);
        var (service, coverStore, fetcher) = CreateService(dbContext);
        fetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);

        await service.RefreshProviderCopyAsync(user, PictureUrl, CancellationToken.None);

        await coverStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default!, default);
        (await dbContext.Users.AsNoTracking().SingleAsync()).ProviderPictureUrl.ShouldBeNull();
    }

    [Fact]
    public async Task SetOverrideAsync_ReplacingAnUpload_UsesANewKeyAndDeletesThePreviousOne()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, pictureUrl: PictureUrl, providerKey: "avatar/provider");
        var (service, coverStore, _) = CreateService(dbContext);

        (await service.SetOverrideAsync(CreatePng(600, 400), CancellationToken.None)).ShouldBeTrue();
        var first = (await dbContext.Users.AsNoTracking().SingleAsync()).AvatarOverrideObjectKey.ShouldNotBeNull();
        (await service.SetOverrideAsync(CreatePng(600, 400), CancellationToken.None)).ShouldBeTrue();
        var second = (await dbContext.Users.AsNoTracking().SingleAsync()).AvatarOverrideObjectKey.ShouldNotBeNull();

        second.ShouldNotBe(first);
        CoverObjectKey.AvatarUserIdOf(second).ShouldBe(TestUsers.Owner);
        await coverStore.Received(1).DeleteAsync(first, Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync("avatar/provider", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_SomethingThatIsNotAnImage_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, pictureUrl: null, providerKey: null);
        var (service, coverStore, _) = CreateService(dbContext);

        await Should.ThrowAsync<InvalidInputException>(
            () => service.SetOverrideAsync("not an image"u8.ToArray(), CancellationToken.None));

        await coverStore.DidNotReceiveWithAnyArgs().SaveAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task RemoveOverrideAsync_AnUploadedAvatar_FallsBackToTheProviderCopy()
    {
        await using var dbContext = CreateDbContext();
        await SeedUserAsync(dbContext, pictureUrl: PictureUrl, providerKey: "avatar/provider", overrideKey: "avatar/upload");
        var (service, coverStore, _) = CreateService(dbContext);

        (await service.RemoveOverrideAsync(CancellationToken.None)).ShouldBeTrue();

        var saved = await dbContext.Users.AsNoTracking().SingleAsync();
        saved.AvatarOverrideObjectKey.ShouldBeNull();
        saved.ProviderAvatarObjectKey.ShouldBe("avatar/provider");
        await coverStore.Received(1).DeleteAsync("avatar/upload", Arg.Any<CancellationToken>());
        await coverStore.DidNotReceive().DeleteAsync("avatar/provider", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOverrideAsync_TheUsersRowIsGone_ReturnsFalse()
    {
        await using var dbContext = CreateDbContext();
        var (service, _, _) = CreateService(dbContext);

        (await service.RemoveOverrideAsync(CancellationToken.None)).ShouldBeFalse();
    }

    private static (AvatarCommandService Service, ICoverStore CoverStore, IProviderAvatarFetcher Fetcher) CreateService(
        LnShelfDbContext dbContext)
    {
        var coverStore = Substitute.For<ICoverStore>();
        var fetcher = Substitute.For<IProviderAvatarFetcher>();
        fetcher.FetchAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(new byte[] { 1, 2, 3 });

        var service = new AvatarCommandService(
            dbContext,
            TestUsers.Current,
            coverStore,
            new CoverNormalizer(Options.Create(new CoverImageOptions()), NullLogger<CoverNormalizer>.Instance),
            fetcher,
            Options.Create(new AvatarOptions()),
            NullLogger<AvatarCommandService>.Instance);

        return (service, coverStore, fetcher);
    }

    private static async Task<User> SeedUserAsync(
        LnShelfDbContext dbContext,
        string? pictureUrl,
        string? providerKey,
        string? overrideKey = null)
    {
        var user = new User
        {
            Id = TestUsers.Owner,
            Issuer = "https://accounts.google.com",
            Subject = "sub-owner",
            Email = "owner@example.com",
            ProviderPictureUrl = pictureUrl,
            ProviderAvatarObjectKey = providerKey,
            AvatarOverrideObjectKey = overrideKey,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return stream.ToArray();
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"avatar-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
