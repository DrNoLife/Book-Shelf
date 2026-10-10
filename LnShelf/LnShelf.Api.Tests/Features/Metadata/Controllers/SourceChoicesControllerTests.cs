using LnShelf.Api.Features.Metadata.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace LnShelf.Api.Tests.Features.Metadata.Controllers;

/// <summary>
/// A reset names the field it hands back. Without one the request is refused rather than
/// binding to the enum's zero value and resetting the volume count choice.
/// </summary>
// A controller test, which testing.md would normally call ceremony: the guard lives in the
// controller, so this is where it can be asserted.
public sealed class SourceChoicesControllerTests
{
    [Fact]
    public async Task ResetAsync_WithoutAField_IsABadRequestAndResetsNothing()
    {
        var commandService = Substitute.For<ISourceChoiceCommandService>();
        var controller = new SourceChoicesController(Substitute.For<ISourceConflictQueryService>(), commandService);

        var result = await controller.ResetAsync(Guid.CreateVersion7(), field: null, volumeNumber: null, CancellationToken.None);

        result.Result.ShouldBeOfType<BadRequestResult>();
        await commandService.DidNotReceiveWithAnyArgs().ResetAsync(default, default, default, default);
    }
}
