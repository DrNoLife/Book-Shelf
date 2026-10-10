using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Models;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using LnShelf.Shared.Sharing;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Services;

/// <summary>
/// Private and Public, and "New link": the row is the setting, so turning sharing off kills
/// the link for good and turning it back on never revives an old one.
/// </summary>
public sealed class ShelfShareCommandServiceTests : IDisposable
{
    private readonly SharingHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task SetVisibilityAsync_Public_CreatesAWellFormedToken()
    {
        var sharing = await SetVisibilityAsync(isPublic: true);

        sharing.IsPublic.ShouldBeTrue();
        ShareToken.IsWellFormed(sharing.Token).ShouldBeTrue();
    }

    // Idempotent: a second "Public" keeps the link people already have.
    [Fact]
    public async Task SetVisibilityAsync_PublicTwice_KeepsTheSameToken()
    {
        var first = await SetVisibilityAsync(isPublic: true);
        var second = await SetVisibilityAsync(isPublic: true);

        second.Token.ShouldBe(first.Token);
    }

    [Fact]
    public async Task SetVisibilityAsync_Private_RemovesTheShare()
    {
        await SetVisibilityAsync(isPublic: true);

        var sharing = await SetVisibilityAsync(isPublic: false);

        sharing.ShouldBe(new ShelfSharingResponse { IsPublic = false, Token = null });
        (await GetAsync()).IsPublic.ShouldBeFalse();
    }

    // Decision D2: an old link never comes back.
    [Fact]
    public async Task SetVisibilityAsync_PrivateThenPublic_IssuesANewToken()
    {
        var first = await SetVisibilityAsync(isPublic: true);
        await SetVisibilityAsync(isPublic: false);

        var again = await SetVisibilityAsync(isPublic: true);

        again.Token.ShouldNotBeNull().ShouldNotBe(first.Token);
    }

    [Fact]
    public async Task RegenerateAsync_WhilePublic_ReplacesTheToken()
    {
        var first = await SetVisibilityAsync(isPublic: true);

        var regenerated = await RegenerateAsync();

        regenerated.IsPublic.ShouldBeTrue();
        regenerated.Token.ShouldNotBeNull().ShouldNotBe(first.Token);
        (await GetAsync()).Token.ShouldBe(regenerated.Token);
    }

    [Fact]
    public async Task RegenerateAsync_WhilePrivate_ThrowsConflict()
    {
        await Should.ThrowAsync<ConflictException>(RegenerateAsync);
    }

    private Task<ShelfSharingResponse> SetVisibilityAsync(bool isPublic) =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareCommandService>().SetVisibilityAsync(isPublic, CancellationToken.None));

    private Task<ShelfSharingResponse> RegenerateAsync() =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareCommandService>().RegenerateAsync(CancellationToken.None));

    private Task<ShelfSharingResponse> GetAsync() =>
        _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareQueryService>().GetAsync(CancellationToken.None));
}
