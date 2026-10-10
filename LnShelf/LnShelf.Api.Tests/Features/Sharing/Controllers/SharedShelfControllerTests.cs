using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace LnShelf.Api.Tests.Features.Sharing.Controllers;

/// <summary>
/// A per-volume cover key is refused before anything else happens: uploads and volume art
/// are never public, whatever the token.
/// </summary>
// A controller test, which testing.md would normally call ceremony. This one guards the only
// check that keeps an anonymous route away from user-uploaded images, and the guard lives in
// the controller rather than the service, so this is where it can be asserted.
public sealed class SharedShelfControllerTests
{
    [Theory]
    [InlineData("override/volume/0199a0f1-1111-7000-8000-000000000001/3f2b0c1d2e3f4a5b6c7d8e9f00112233.png")]
    [InlineData("volume/apple-books/0199a0f1-1111-7000-8000-000000000001.jpg")]
    [InlineData("../anilist/1001.jpg")]
    public async Task GetCoverAsync_VolumeOverrideOrMalformedKey_IsNotFound(string objectKey)
    {
        var sharedShelf = Substitute.For<ISharedShelfQueryService>();
        sharedShelf.IsCoverOnShelfAsync(default!, default!, default).ReturnsForAnyArgs(true);
        var coverStore = Substitute.For<ICoverStore>();
        var controller = new SharedShelfController(sharedShelf, coverStore);

        var result = await controller.GetCoverAsync("AAAAAAAAAAAAAAAAAAAAAA", objectKey, CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
        await sharedShelf.DidNotReceiveWithAnyArgs().IsCoverOnShelfAsync(default!, default!, default);
        await coverStore.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
    }
}
