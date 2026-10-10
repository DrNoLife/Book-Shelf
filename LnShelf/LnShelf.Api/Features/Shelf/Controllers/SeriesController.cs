namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/series")]
public sealed class SeriesController(
    ISeriesCommandService seriesCommandService,
    ISeriesDetailQueryService seriesDetailQueryService,
    ITrackedSeriesQueryService trackedSeriesQueryService,
    IVolumeCommandService volumeCommandService,
    IVolumeReadDateCommandService volumeReadDateCommandService) : ControllerBase
{
    /// <summary>
    /// The external ids of the series already on the shelf, for the search page's
    /// "on shelf" marker.
    /// </summary>
    // The {seriesId:guid} constraint keeps "tracked" from ever matching that route.
    [HttpGet("tracked")]
    public async Task<ActionResult<TrackedSeriesIdsResponse>> GetTrackedIdsAsync(
        CancellationToken cancellationToken) =>
        Ok(await trackedSeriesQueryService.GetTrackedIdsAsync(cancellationToken));

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
    /// Sets or clears the read date of several read volumes at once. Answers with the
    /// refreshed series.
    /// </summary>
    [HttpPut("{seriesId:guid}/volumes/read-date")]
    public async Task<ActionResult<SeriesDetailResponse>> SetReadDatesAsync(
        Guid seriesId,
        [FromBody] SetVolumeReadDatesRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await volumeReadDateCommandService.SetManyAsync(seriesId, request, cancellationToken);
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
    /// Sets the series' tracking status and note. Every omitted field is left alone.
    /// Answers with the refreshed series.
    /// </summary>
    [HttpPut("{seriesId:guid}/state")]
    public async Task<ActionResult<SeriesDetailResponse>> SetStateAsync(
        Guid seriesId,
        [FromBody] UpdateSeriesStateRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await seriesCommandService.SetStateAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Acknowledges the new-volume badge: the user has now seen this series.
    /// </summary>
    // 204 rather than the refreshed series, unlike the other writes here. This one is not
    // an edit the reader made — it is the page reporting that it rendered — and answering
    // with a whole series the client already holds would make every visit pay for a
    // response nobody reads.
    [HttpPost("{seriesId:guid}/seen")]
    public async Task<IActionResult> MarkSeenAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var seen = await seriesCommandService.MarkSeenAsync(seriesId, cancellationToken);
        return seen ? NoContent() : NotFound();
    }

    /// <summary>
    /// Adds a series to the shelf from whichever source the request names, with its
    /// volumes and a default user-state row (feature-spec §6).
    /// </summary>
    // One endpoint for every source rather than a route each, deliberately. The search page
    // has one row shape and one Add button; what differs is which id the row was carrying.
    // A second route would duplicate this method's whole contract — the 201/200 idempotency
    // evidence and the 404 shape — to express a fact the body already states.
    //
    // The status code is that idempotency evidence: 201 the first time, 200 with
    // alreadyTracked every time after. No Location header yet — GET /api/series/{id} is
    // phase 3, and pointing at a route that does not exist would be a lie in a header.
    //
    // A request naming neither source or both is a 400, from AddSeriesSource; so is one
    // naming a source no IAddSeriesFlow is registered for. Which flow runs is the command
    // service's lookup, not a switch here — nothing about dispatching to a metadata source
    // is a controller's business.
    [EnableRateLimiting(UserRateLimits.ExternalSources)]
    [HttpPost]
    public async Task<ActionResult<AddSeriesResponse>> AddAsync(
        [FromBody] AddSeriesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await seriesCommandService.AddAsync(request, cancellationToken);

        if (response is null)
        {
            // Re-derived rather than carried down and back: it is a pure read of the two
            // ids on the request, and the add already ran without needing the controller
            // to have decided anything.
            var source = AddSeriesSource.Of(request);

            var sourceId = request.AniListId?.ToString(CultureInfo.InvariantCulture)
                           ?? request.MangaUpdatesId?.ToString(CultureInfo.InvariantCulture);

            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = $"Unknown {source} series",
                Detail = $"{source} has no series with id {sourceId}.",
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
