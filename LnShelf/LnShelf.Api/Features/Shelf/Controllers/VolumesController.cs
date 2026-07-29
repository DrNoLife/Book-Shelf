namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/volumes")]
public sealed class VolumesController(IVolumeCommandService volumeCommandService) : ControllerBase
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
