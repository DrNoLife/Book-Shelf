namespace LnShelf.Api.Features.Recommendations.Controllers;

/// <summary>
/// "Not interested": the works hidden from For you, and showing them again.
/// </summary>
// No rate limit: nothing here reaches an external source.
[ApiController]
[Route("api/recommendations/dismissals")]
public sealed class RecommendationDismissalsController(
    IRecommendationDismissalQueryService dismissalQuery,
    IRecommendationDismissalCommandService dismissalCommand) : ControllerBase
{
    /// <summary>The hidden works, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecommendationDismissalResponse>>> ListAsync(CancellationToken cancellationToken) =>
        Ok(await dismissalQuery.ListAsync(cancellationToken));

    /// <summary>Hides a work. Hiding one that is hidden already is not an error.</summary>
    [HttpPut("{aniListId:int}")]
    public async Task<IActionResult> DismissAsync(
        int aniListId,
        [FromBody] DismissRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        await dismissalCommand.DismissAsync(aniListId, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Shows a work again. 404 when it was not hidden.</summary>
    [HttpDelete("{aniListId:int}")]
    public async Task<IActionResult> RestoreAsync(int aniListId, CancellationToken cancellationToken) =>
        await dismissalCommand.RestoreAsync(aniListId, cancellationToken) ? NoContent() : NotFound();
}
