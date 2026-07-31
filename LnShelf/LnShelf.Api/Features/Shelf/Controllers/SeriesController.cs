namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/series")]
public sealed class SeriesController(
    ISeriesCommandService seriesCommandService,
    ISeriesDetailQueryService seriesDetailQueryService,
    IVolumeCommandService volumeCommandService) : ControllerBase
{
    /// <summary>
    /// Everything the series-detail page renders, in one round trip (feature-spec §6).
    /// </summary>
    [HttpGet("{seriesId:guid}")]
    public async Task<ActionResult<SeriesDetailResponse>> GetAsync(
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var detail = await seriesDetailQueryService.GetAsync(seriesId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// "Mark 1–8 read" / "Mark 1–11 owned". Returns the refreshed series so the page
    /// updates from one response rather than re-fetching.
    /// </summary>
    [HttpPost("{seriesId:guid}/volumes/bulk")]
    public async Task<ActionResult<SeriesDetailResponse>> SetBulkAsync(
        Guid seriesId,
        [FromBody] BulkVolumeStateRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await volumeCommandService.SetBulkAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Sets this series' volume view, or clears the override with a null view so the
    /// series follows the global preference. Answers with the refreshed series.
    /// </summary>
    [HttpPut("{seriesId:guid}/volume-view")]
    public async Task<ActionResult<SeriesDetailResponse>> SetVolumeViewAsync(
        Guid seriesId,
        [FromBody] SetSeriesVolumeViewRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await seriesCommandService.SetVolumeViewAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Adds an AniList series to the shelf, with its volumes and a default
    /// user-state row (feature-spec §6).
    /// </summary>
    // The status code is the idempotency evidence: 201 the first time, 200 with
    // alreadyTracked every time after. No Location header yet — GET
    // /api/series/{id} is phase 3, and pointing at a route that does not exist
    // would be a lie in a header.
    [HttpPost]
    public async Task<ActionResult<AddSeriesResponse>> AddAsync(
        [FromBody] AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        var response = await seriesCommandService.AddFromAniListAsync(request, cancellationToken);

        if (response is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Unknown AniList series",
                Detail = $"AniList has no series with id {request.AniListId}.",
            });
        }

        return response.AlreadyTracked
            ? Ok(response)
            : StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Removes a series from the shelf, with its volumes and all user state on them
    /// (feature-spec §6).
    /// </summary>
    // 404 rather than 204 for an id that is not there. The two answers are different
    // facts — "it is gone now" and "it was never here" — and the second one is worth
    // knowing when a stale page sends the request twice.
    [HttpDelete("{seriesId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var deleted = await seriesCommandService.DeleteAsync(seriesId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
