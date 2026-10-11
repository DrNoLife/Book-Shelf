namespace LnShelf.Api.Features.Shelf.Controllers;

/// <summary>
/// Series the reader keeps by hand: the add for a series no catalogue knows, and its volumes.
/// </summary>
// Its own controller under the same route prefix rather than more actions on SeriesController:
// these are the only endpoints that write volumes into existence, and keeping them together
// keeps the "only for a series with no external id" rule in one place.
[ApiController]
[Route("api/series")]
public sealed class ManualSeriesController(IManualSeriesCommandService manualSeriesCommandService) : ControllerBase
{
    /// <summary>
    /// Adds a series with no external id — a web novel, or anything the search cannot find —
    /// with volumes 1..N out. 201 the first time, 200 with alreadyTracked when the shelf
    /// already holds one by that exact title and format.
    /// </summary>
    [HttpPost("manual")]
    public async Task<ActionResult<AddSeriesResponse>> AddAsync(
        [FromBody] AddManualSeriesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await manualSeriesCommandService.AddAsync(request, cancellationToken);

        return response.AlreadyTracked
            ? Ok(response)
            : StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Adds one volume to a series kept by hand: the next whole number, or the one asked for.
    /// Answers with the refreshed series; 409 for a linked series or a number already there.
    /// </summary>
    [HttpPost("{seriesId:guid}/volumes")]
    public async Task<ActionResult<SeriesDetailResponse>> AddVolumeAsync(
        Guid seriesId,
        [FromBody] AddLocalVolumeRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await manualSeriesCommandService.AddVolumeAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Removes a volume added by hand, with the reader's state on it. Answers with the
    /// refreshed series; 409 for a linked series or a volume a source supplied.
    /// </summary>
    [HttpDelete("{seriesId:guid}/volumes/{volumeId:guid}")]
    public async Task<ActionResult<SeriesDetailResponse>> RemoveVolumeAsync(
        Guid seriesId,
        Guid volumeId,
        CancellationToken cancellationToken)
    {
        var detail = await manualSeriesCommandService.RemoveVolumeAsync(seriesId, volumeId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
