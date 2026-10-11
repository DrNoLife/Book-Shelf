namespace LnShelf.Api.Features.Metadata.Controllers;

/// <summary>
/// Linking a series to its release-source record by hand, when the sync could not decide.
/// </summary>
// Both actions ask the release source, so both spend from the same per-user allowance as search:
// these are external calls a person can trigger as fast as they can click.
[ApiController]
[Route("api/series/{seriesId:guid}/release-link")]
[EnableRateLimiting(UserRateLimits.ExternalSources)]
public sealed class ReleaseLinkController(
    IReleaseLinkQueryService releaseLinkQueryService,
    IReleaseLinkCommandService releaseLinkCommandService) : ControllerBase
{
    /// <summary>What the release source offers for this series, searched by title.</summary>
    /// <param name="query">Bound from <c>q</c>; blank searches the series' own title.</param>
    [HttpGet("candidates")]
    public async Task<ActionResult<ReleaseLinkCandidatesResponse>> GetCandidatesAsync(
        Guid seriesId,
        [FromQuery(Name = "q")] string? query,
        CancellationToken cancellationToken)
    {
        var candidates = await releaseLinkQueryService.GetCandidatesAsync(seriesId, query, cancellationToken);
        return candidates is null ? NotFound() : Ok(candidates);
    }

    /// <summary>
    /// Links the series and refreshes it at once. Answers with the series and whether the
    /// refresh ran.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ReleaseLinkResponse>> LinkAsync(
        Guid seriesId,
        [FromBody] LinkReleaseSourceRequest request,
        CancellationToken cancellationToken)
    {
        var linked = await releaseLinkCommandService.LinkAsync(seriesId, request, cancellationToken);
        return linked is null ? NotFound() : Ok(linked);
    }
}
