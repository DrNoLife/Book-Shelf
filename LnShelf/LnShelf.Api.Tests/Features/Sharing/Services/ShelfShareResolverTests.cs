using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Models;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Services;

/// <summary>
/// The token lookup: the only unfiltered read in the crossing, and it needs no lifted filter.
/// </summary>
public sealed class ShelfShareResolverTests : IDisposable
{
    private readonly SharingHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task FindOwnerAsync_KnownToken_ReturnsItsUser()
    {
        await _harness.SeedTwoShelvesAsync();
        await _harness.ShareAsync(TestUsers.Owner);
        var token = await _harness.ShareAsync(TestUsers.SomeoneElse);

        var owner = await _harness.AsUserAsync(TestUsers.Owner, services => FindOwnerAsync(services, token));

        owner.ShouldBe(TestUsers.SomeoneElse);
    }

    // shelf_share is not owner-filtered by design, so a scope with no user at all still finds
    // the token: no IgnoreQueryFilters is needed, which is the point.
    [Fact]
    public async Task FindOwnerAsync_AnonymousContext_StillResolves()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        var owner = await _harness.AsRequestAsync(services => FindOwnerAsync(services, token));

        owner.ShouldBe(TestUsers.Owner);
    }

    [Fact]
    public async Task FindOwnerAsync_UnknownToken_ReturnsNull()
    {
        await _harness.SeedTwoShelvesAsync();
        await _harness.ShareAsync(TestUsers.Owner);

        var owner = await _harness.AsRequestAsync(services => FindOwnerAsync(services, ShareToken.Create()));

        owner.ShouldBeNull();
    }

    private static Task<Guid?> FindOwnerAsync(IServiceProvider services, string token) =>
        services.GetRequiredService<IShelfShareResolver>().FindOwnerAsync(token, CancellationToken.None);
}
