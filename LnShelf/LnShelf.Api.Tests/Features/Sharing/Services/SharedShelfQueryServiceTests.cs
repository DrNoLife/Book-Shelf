using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Models;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using LnShelf.Shared.Sharing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Services;

/// <summary>
/// The one read that crosses the owner filters (feature-spec §4.8): a token opens exactly the
/// shelf of the user who shared it, as an anonymous request, and nothing else.
/// </summary>
public sealed class SharedShelfQueryServiceTests : IDisposable
{
    private readonly SharingHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    // The leak test: the token's owner's series, and none of anyone else's.
    [Fact]
    public async Task GetAsync_TokenOfUserA_ReturnsOnlyUserAsSeries()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var shelf = (await GetAsync(token)).ShouldNotBeNull();

        Titles(shelf).ShouldBe([SharingHarness.OwnerReadingTitle]);
    }

    // The symmetric case, so the test above cannot pass by "first user wins".
    [Fact]
    public async Task GetAsync_TokenOfUserB_ReturnsOnlyUserBsSeries()
    {
        await _harness.SeedTwoShelvesAsync();
        await _harness.ShareAsync(TestUsers.Owner);
        var token = await _harness.ShareAsync(TestUsers.SomeoneElse);

        var shelf = (await GetAsync(token)).ShouldNotBeNull();

        Titles(shelf).ShouldBe([SharingHarness.SomeoneElsesTitle]);
    }

    [Fact]
    public async Task GetAsync_UnknownWellFormedToken_ReturnsNull()
    {
        await _harness.SeedTwoShelvesAsync();
        await _harness.ShareAsync(TestUsers.Owner);

        (await GetAsync(ShareToken.Create())).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not a token at all, far too long")]
    [InlineData("%%%%%%%%%%%%%%%%%%%%%%")]
    public async Task GetAsync_MalformedToken_ReturnsNull(string token)
    {
        await _harness.SeedTwoShelvesAsync();
        await _harness.ShareAsync(TestUsers.Owner);

        (await GetAsync(token)).ShouldBeNull();
    }

    // A disabled token is a 404 from the next request on.
    [Fact]
    public async Task GetAsync_AfterMadePrivate_ReturnsNull()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        await _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareCommandService>().SetVisibilityAsync(false, CancellationToken.None));

        (await GetAsync(token)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_AfterRegenerate_OldTokenReturnsNullAndNewTokenReturnsTheShelf()
    {
        await _harness.SeedTwoShelvesAsync();
        var oldToken = await _harness.ShareAsync(TestUsers.Owner);

        var regenerated = await _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareCommandService>().RegenerateAsync(CancellationToken.None));

        (await GetAsync(oldToken)).ShouldBeNull();
        var shelf = (await GetAsync(regenerated.Token.ShouldNotBeNull())).ShouldNotBeNull();
        Titles(shelf).ShouldBe([SharingHarness.OwnerReadingTitle]);
    }

    [Fact]
    public async Task GetAsync_DroppedSeries_IsLeftOut()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var shelf = (await GetAsync(token)).ShouldNotBeNull();

        Titles(shelf).ShouldNotContain(SharingHarness.OwnerDroppedTitle);
    }

    // The child scope acts as the owner even though the request carries another user's
    // session: ActAs wins over the claim, which is exactly what the crossing relies on.
    [Fact]
    public async Task GetAsync_VisitorSignedInAsUserB_SeesUserAsShelfNotTheirOwn()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);
        _harness.SignInVisitor(TestUsers.SomeoneElse);

        var shelf = (await GetAsync(token)).ShouldNotBeNull();

        Titles(shelf).ShouldBe([SharingHarness.OwnerReadingTitle]);
    }

    // The request's own scope is never touched: nothing after the read in this request runs
    // as the owner.
    [Fact]
    public async Task GetAsync_AfterReading_LeavesTheRequestScopeAnonymous()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);
        await using var request = _harness.CreateRequestScope();

        (await request.ServiceProvider.GetRequiredService<ISharedShelfQueryService>().GetAsync(token, CancellationToken.None))
            .ShouldNotBeNull();

        request.ServiceProvider.GetRequiredService<ICurrentUser>().IdOrNull.ShouldBeNull();
        (await request.ServiceProvider.GetRequiredService<LnShelfDbContext>().Series.CountAsync()).ShouldBe(0);
    }

    // Owning a volume proves it is out, but counting it would let the out count give away
    // what the owner has bought (decision D5).
    [Fact]
    public async Task GetAsync_OwnedButUnreadVolumeTheSourceHasNotReleased_DoesNotCountAsOut()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            SharingHarness.NewSeries("Owned only", TrackingStatus.Reading, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Unread, owned: true, isReleased: false)));
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var card = (await GetAsync(token)).ShouldNotBeNull().Series.ShouldHaveSingleItem();

        card.Out.ShouldBe(0);
        card.Read.ShouldBe(0);
    }

    // You cannot read a volume that is not out, so a read volume counts as out.
    [Fact]
    public async Task GetAsync_ReadVolume_CountsAsOutAndRead()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            SharingHarness.NewSeries("Read only", TrackingStatus.Reading, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Read, owned: false, isReleased: false)));
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var card = (await GetAsync(token)).ShouldNotBeNull().Series.ShouldHaveSingleItem();

        card.Read.ShouldBe(1);
        card.Out.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_Totals_CountVolumesReadAndSeriesReadingAndCompleted()
    {
        await _harness.AddSeriesAsync(
            TestUsers.Owner,
            SharingHarness.NewSeries("Reading", TrackingStatus.Reading, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Read, owned: false, isReleased: true),
                SharingHarness.NewVolume(2, ReadStatus.Read, owned: false, isReleased: true)),
            SharingHarness.NewSeries("Completed", TrackingStatus.Completed, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Read, owned: false, isReleased: true)),
            SharingHarness.NewSeries("Waiting", status: null, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Unread, owned: false, isReleased: true)),
            SharingHarness.NewSeries("Dropped", TrackingStatus.Dropped, cover: null,
                SharingHarness.NewVolume(1, ReadStatus.Read, owned: false, isReleased: true)));
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var shelf = (await GetAsync(token)).ShouldNotBeNull();

        shelf.Totals.ShouldBe(new SharedShelfTotals { VolumesRead = 3, SeriesReading = 1, SeriesCompleted = 1 });
        Titles(shelf).ShouldBe(["Reading", "Completed", "Waiting"]);
    }

    private Task<SharedShelfResponse?> GetAsync(string token) =>
        _harness.AsRequestAsync(
            services => services.GetRequiredService<ISharedShelfQueryService>().GetAsync(token, CancellationToken.None));

    private static string[] Titles(SharedShelfResponse shelf) => [.. shelf.Series.Select(card => card.TitleEnglish)];
}
