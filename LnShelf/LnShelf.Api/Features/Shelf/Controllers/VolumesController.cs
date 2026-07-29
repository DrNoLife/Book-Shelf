using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/volumes")]
public sealed class VolumesController(
    IVolumeCommandService volumeCommandService,
    IOptions<CoverStoreOptions> coverOptions) : ControllerBase
{
    /// <summary>
    /// Sets a volume's read status (feature-spec §6). The tile decides what the next
    /// state in the cycle is; this records it.
    /// </summary>
    [HttpPut("{volumeId:guid}/read")]
    public async Task<ActionResult<VolumeResponse>> SetReadAsync(
        Guid volumeId,
        [FromBody] SetVolumeReadRequest request,
        CancellationToken cancellationToken)
    {
        var volume = await volumeCommandService.SetReadAsync(volumeId, request, cancellationToken);
        return volume is null ? NotFound() : Ok(volume);
    }

    /// <summary>
    /// Stores a user-supplied cover for this volume (feature-spec §6). Every cover in
    /// the UI is a drop target, and this is where the drop lands.
    /// </summary>
    [HttpPut("{volumeId:guid}/cover")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<VolumeResponse>> SetCoverAsync(
        Guid volumeId,
        IFormFile file,
        CancellationToken cancellationToken)
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

        var volume = await volumeCommandService.SetCoverAsync(volumeId, buffer.ToArray(), cancellationToken);
        return volume is null ? NotFound() : Ok(volume);
    }

    /// <summary>Reverts to whatever a metadata source supplied.</summary>
    [HttpDelete("{volumeId:guid}/cover")]
    public async Task<ActionResult<VolumeResponse>> RemoveCoverAsync(
        Guid volumeId,
        CancellationToken cancellationToken)
    {
        var volume = await volumeCommandService.RemoveCoverAsync(volumeId, cancellationToken);
        return volume is null ? NotFound() : Ok(volume);
    }

    /// <summary>Toggles ownership, with an optional format and price.</summary>
    [HttpPut("{volumeId:guid}/owned")]
    public async Task<ActionResult<VolumeResponse>> SetOwnedAsync(
        Guid volumeId,
        [FromBody] SetVolumeOwnedRequest request,
        CancellationToken cancellationToken)
    {
        var volume = await volumeCommandService.SetOwnedAsync(volumeId, request, cancellationToken);
        return volume is null ? NotFound() : Ok(volume);
    }
}
