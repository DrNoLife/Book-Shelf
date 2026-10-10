using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Tests.Features.Sharing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Sharing.Services;

/// <summary>
/// The anonymous cover route serves the series art of a series on the shared shelf, and
/// nothing else: not another user's art, not a dropped series', not after a revoke.
/// </summary>
public sealed class SharedShelfCoverTests : IDisposable
{
    private readonly SharingHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task IsCoverOnShelfAsync_SeriesArtOnTheSharedShelf_IsTrue()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        (await IsCoverOnShelfAsync(token, SharingHarness.OwnerReadingCover)).ShouldBeTrue();
    }

    // Someone else's key through the owner's token: their shelf is not this one.
    [Fact]
    public async Task IsCoverOnShelfAsync_AnotherUsersSeriesArt_IsFalse()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        (await IsCoverOnShelfAsync(token, SharingHarness.SomeoneElsesCover)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsCoverOnShelfAsync_DroppedSeriesArt_IsFalse()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);

        (await IsCoverOnShelfAsync(token, SharingHarness.OwnerDroppedCover)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsCoverOnShelfAsync_RevokedToken_IsFalse()
    {
        await _harness.SeedTwoShelvesAsync();
        var token = await _harness.ShareAsync(TestUsers.Owner);
        await _harness.AsUserAsync(
            TestUsers.Owner,
            services => services.GetRequiredService<IShelfShareCommandService>().SetVisibilityAsync(false, CancellationToken.None));

        (await IsCoverOnShelfAsync(token, SharingHarness.OwnerReadingCover)).ShouldBeFalse();
    }

    private Task<bool> IsCoverOnShelfAsync(string token, string objectKey) =>
        _harness.AsRequestAsync(
            services => services.GetRequiredService<ISharedShelfQueryService>()
                .IsCoverOnShelfAsync(token, objectKey, CancellationToken.None));
}
