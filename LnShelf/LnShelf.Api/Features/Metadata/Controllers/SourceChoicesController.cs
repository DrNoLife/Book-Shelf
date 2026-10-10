namespace LnShelf.Api.Features.Metadata.Controllers;

/// <summary>
/// What each source said about a series, and the user's pick when they disagree.
/// </summary>
[ApiController]
[Route("api/series/{seriesId:guid}")]
public sealed class SourceChoicesController(
    ISourceConflictQueryService sourceConflictQueryService,
    ISourceChoiceCommandService sourceChoiceCommandService) : ControllerBase
{
    /// <summary>
    /// The side-by-side view: every disagreement on this series from the last sweep, with
    /// formatted values and which source the shelf shows.
    /// </summary>
    [HttpGet("conflicts")]
    public async Task<ActionResult<IReadOnlyList<SourceConflictResponse>>> GetConflictsAsync(
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var conflicts = await sourceConflictQueryService.GetAsync(seriesId, cancellationToken);
        return conflicts is null ? NotFound() : Ok(conflicts);
    }

    /// <summary>Sets the choice. Answers with the refreshed series.</summary>
    [HttpPut("source-choice")]
    public async Task<ActionResult<SeriesDetailResponse>> ChooseAsync(
        Guid seriesId,
        [FromBody] ChooseSourceRequest request,
        CancellationToken cancellationToken)
    {
        var detail = await sourceChoiceCommandService.ChooseAsync(seriesId, request, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>Hands the field back to the usual rules. Answers with the refreshed series.</summary>
    [HttpDelete("source-choice")]
    public async Task<ActionResult<SeriesDetailResponse>> ResetAsync(
        Guid seriesId,
        [FromQuery] SyncDiscrepancyField? field,
        [FromQuery] int? volumeNumber,
        CancellationToken cancellationToken)
    {
        // A missing field must not bind to the enum's zero value and quietly reset the volume
        // count choice, which is user data.
        if (field is null)
        {
            return BadRequest();
        }

        var detail = await sourceChoiceCommandService.ResetAsync(seriesId, field.Value, volumeNumber, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
