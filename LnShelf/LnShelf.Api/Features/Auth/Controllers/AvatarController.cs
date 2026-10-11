namespace LnShelf.Api.Features.Auth.Controllers;

/// <summary>
/// The signed-in user's own avatar: read it, upload one, or go back to the provider's picture.
/// There is no route to anyone else's.
/// </summary>
[ApiController]
[Route("api/auth/me/avatar")]
public sealed class AvatarController(
    IAvatarQueryService avatarQuery,
    IAvatarCommandService avatarCommand,
    ICurrentUserQueryService currentUserQuery,
    IOptions<CoverImageOptions> coverOptions) : ControllerBase
{
    /// <summary>The uploaded picture, else the provider's copy; 404 when there is neither.</summary>
    /// <param name="version">The AvatarVersion the client was given; see below.</param>
    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery(Name = "v")] string? version, CancellationToken cancellationToken)
    {
        var avatar = await avatarQuery.GetCurrentAsync(cancellationToken);
        if (avatar is null)
        {
            return NotFound();
        }

        // Kept for a week under the URL that names its version, as covers are, because a new
        // picture gets a new version and so a new URL. Asked for under any other version it
        // must not be kept, or that URL would hold whatever picture happened to be current.
        // Private either way: the Cloudflare edge must never hand one user's face to another.
        Response.Headers.CacheControl = string.Equals(version, avatar.Version, StringComparison.Ordinal)
            ? "private, max-age=604800, immutable"
            : "private, no-cache";

        return File(avatar.Content, avatar.ContentType);
    }

    /// <summary>Stores an uploaded picture as the avatar, over the provider's.</summary>
    // The cover upload's limits, because it is the same kind of object from the same kind of
    // drop target; only the size it is normalised to differs.
    [HttpPut]
    [RequestSizeLimit(CoverImageOptions.UploadCeilingBytes)]
    public async Task<ActionResult<CurrentUserResponse>> SetAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidInputException("Attach an image as 'file'.");
        }

        if (file.Length > coverOptions.Value.MaximumUploadBytes)
        {
            throw new InvalidInputException(
                $"That image is {file.Length / 1024} KB; the limit is {coverOptions.Value.MaximumUploadBytes / 1024} KB.");
        }

        using var buffer = new MemoryStream();
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(buffer, cancellationToken);
        }

        if (!await avatarCommand.SetOverrideAsync(buffer.ToArray(), cancellationToken))
        {
            return NotFound();
        }

        return await CurrentUserAsync(cancellationToken);
    }

    /// <summary>Removes the uploaded picture; the provider's copy, if any, shows again.</summary>
    [HttpDelete]
    public async Task<ActionResult<CurrentUserResponse>> RemoveAsync(CancellationToken cancellationToken)
    {
        if (!await avatarCommand.RemoveOverrideAsync(cancellationToken))
        {
            return NotFound();
        }

        return await CurrentUserAsync(cancellationToken);
    }

    // The writes answer with who is signed in, carrying the new AvatarVersion, so the client
    // swaps the picture without a second request.
    private async Task<ActionResult<CurrentUserResponse>> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await currentUserQuery.GetAsync(cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }
}
