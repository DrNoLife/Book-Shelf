using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using LnShelf.Shared.Sharing;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Services;

/// <summary>The signed-in user's own setting, and only theirs.</summary>
public sealed class ShelfShareQueryServiceTests : IDisposable
{
    private readonly SharingHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetAsync_NoShare_IsPrivateWithNoToken()
    {
        (await GetAsync(TestUsers.Owner)).ShouldBe(new ShelfSharingResponse { IsPublic = false, Token = null });
    }

    [Fact]
    public async Task GetAsync_Shared_ReturnsTheToken()
    {
        var token = await _harness.ShareAsync(TestUsers.Owner);

        (await GetAsync(TestUsers.Owner)).ShouldBe(new ShelfSharingResponse { IsPublic = true, Token = token });
    }

    // Someone else's link is theirs: it neither shows here nor makes this shelf public.
    [Fact]
    public async Task GetAsync_OnlyAnotherUserShares_IsPrivate()
    {
        await _harness.ShareAsync(TestUsers.SomeoneElse);

        (await GetAsync(TestUsers.Owner)).ShouldBe(new ShelfSharingResponse { IsPublic = false, Token = null });
    }

    private Task<ShelfSharingResponse> GetAsync(Guid userId) =>
        _harness.AsUserAsync(
            userId,
            services => services.GetRequiredService<IShelfShareQueryService>().GetAsync(CancellationToken.None));
}
