namespace LnShelf.Api.Features.Shelf.Controllers;

[ApiController]
[Route("api/series")]
public sealed class SeriesController(ISeriesCommandService seriesCommandService) : ControllerBase
{
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
}
