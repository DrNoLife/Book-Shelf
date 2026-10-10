using LnShelf.Api.Features.Sharing.Abstractions;

namespace LnShelf.Api.Features.Sharing.Controllers;

/// <summary>
/// The signed-in user's "Who can see your shelf" setting (feature-spec §4.8).
/// </summary>
[ApiController]
[Route("api/sharing")]
public sealed class ShelfSharingController(
    IShelfShareQueryService shareQuery,
    IShelfShareCommandService shareCommand) : ControllerBase
{
    /// <summary>Private or public, and the link's token while public.</summary>
    [HttpGet]
    public async Task<ActionResult<ShelfSharingResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await shareQuery.GetAsync(cancellationToken));

    /// <summary>Public issues a link (or keeps the current one); private deletes it.</summary>
    [HttpPut]
    public async Task<ActionResult<ShelfSharingResponse>> UpdateAsync(
        [FromBody] UpdateShelfSharingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Ok(await shareCommand.SetVisibilityAsync(request.IsPublic, cancellationToken));
    }

    /// <summary>A new link; the old one is a 404 from the next request on. 409 while private.</summary>
    [HttpPost("regenerate")]
    public async Task<ActionResult<ShelfSharingResponse>> RegenerateAsync(CancellationToken cancellationToken) =>
        Ok(await shareCommand.RegenerateAsync(cancellationToken));
}
