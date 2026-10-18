namespace LnShelf.Api.Features.Covers.Controllers;

/// <summary>
/// The covers users share with each other (feature-spec §4.3). Signed-in users only, like
/// every route that does not say <c>[AllowAnonymous]</c>.
/// </summary>
[ApiController]
public sealed class CommunityCoversController(
    ICommunityCoverQueryService communityCoverQuery,
    ICommunityCoverCommandService communityCoverCommand) : ControllerBase
{
    /// <summary>What others share for this volume. 404 when the volume is not the user's.</summary>
    [HttpGet("api/volumes/{volumeId:guid}/community-covers")]
    public async Task<ActionResult<CommunityCoversResponse>> ListAsync(
        Guid volumeId,
        CancellationToken cancellationToken)
    {
        var response = await communityCoverQuery.ListForVolumeAsync(volumeId, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    /// <summary>Streams a shared cover by the hash of its bytes.</summary>
    // Exactly 64 lowercase hex characters, the shape of a hash, so nothing else reaches the
    // lookup. Private for the reason CoversController's header is: no shared cache may hold
    // an image that only signed-in users may fetch.
    [HttpGet("api/community-covers/{contentHash:regex(^[[0-9a-f]]{{64}}$)}")]
    public async Task<IActionResult> GetImageAsync(string contentHash, CancellationToken cancellationToken)
    {
        var cover = await communityCoverQuery.ReadImageAsync(contentHash, cancellationToken);
        if (cover is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=604800, immutable";

        return File(cover.Content, cover.ContentType);
    }

    /// <summary>Stops offering this cover to the user. Idempotent.</summary>
    [HttpPut("api/community-covers/{contentHash:regex(^[[0-9a-f]]{{64}}$)}/hide")]
    public async Task<IActionResult> HideAsync(string contentHash, CancellationToken cancellationToken)
    {
        await communityCoverCommand.HideAsync(contentHash, cancellationToken);
        return NoContent();
    }

    /// <summary>Offers a hidden cover again. Idempotent.</summary>
    [HttpDelete("api/community-covers/{contentHash:regex(^[[0-9a-f]]{{64}}$)}/hide")]
    public async Task<IActionResult> UnhideAsync(string contentHash, CancellationToken cancellationToken)
    {
        await communityCoverCommand.UnhideAsync(contentHash, cancellationToken);
        return NoContent();
    }

    /// <summary>Whether the user shares their uploaded covers, and since when.</summary>
    [HttpGet("api/community-covers/sharing")]
    public async Task<ActionResult<CoverSharingResponse>> GetSharingAsync(CancellationToken cancellationToken) =>
        Ok(await communityCoverQuery.GetSharingAsync(cancellationToken));

    /// <summary>
    /// On publishes the covers already uploaded and every one from now on; off withdraws them
    /// all. Copies other users already picked stay on their shelves.
    /// </summary>
    [HttpPut("api/community-covers/sharing")]
    public async Task<ActionResult<CoverSharingResponse>> SetSharingAsync(
        [FromBody] SetCoverSharingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Ok(await communityCoverCommand.SetSharingAsync(request.Enabled, cancellationToken));
    }
}
