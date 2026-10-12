namespace LnShelf.Api.Features.Shelf.Controllers;

/// <summary>Chapter progress on the series page: the unit a series is counted in, and the last chapter read.</summary>
// Its own controller beside SeriesController, as the notification mute has: the two writes
// share one command service, and SeriesController's constructor already carries five.
[ApiController]
[Route("api/series")]
public sealed class SeriesProgressController(
    ISeriesProgressCommandService seriesProgressCommandService) : ControllerBase
{
    /// <summary>
    /// Counts the series in volumes or in chapters. 400 for chapters on a light novel. Answers
    /// with the refreshed series.
    /// </summary>
    [HttpPut("{seriesId:guid}/progress-unit")]
    public async Task<ActionResult<SeriesDetailResponse>> SetProgressUnitAsync(
        Guid seriesId,
        [FromBody] SetProgressUnitRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await seriesProgressCommandService.SetProgressUnitAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Records the last chapter read, or clears it with a null. 400 for a number out of range
    /// and for a light novel. Answers with the refreshed series.
    /// </summary>
    [HttpPut("{seriesId:guid}/chapter")]
    public async Task<ActionResult<SeriesDetailResponse>> SetLastChapterReadAsync(
        Guid seriesId,
        [FromBody] SetLastChapterReadRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await seriesProgressCommandService.SetLastChapterReadAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
