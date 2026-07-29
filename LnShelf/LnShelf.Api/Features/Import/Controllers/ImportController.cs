using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Import.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ImportController(
    IShelfImporter shelfImporter,
    IImportQueryService importQueryService,
    IImportCommandService importCommandService,
    IOptions<ImportOptions> options) : ControllerBase
{
    /// <summary>
    /// Uploads a library export and stages it: parsed, grouped by series, and
    /// matched against the metadata source. Writes nothing to the shelf.
    /// </summary>
    // Slow by nature — one metadata search plus a few cover fetches per series —
    // and deliberately synchronous: a background job would need a progress channel
    // and a way to resume, which one user importing one file does not justify.
    [HttpPost]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<ImportBatchResponse>> UploadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidInputException("Attach a StoryGraph CSV export as 'file'.");
        }

        if (file.Length > options.Value.MaximumFileBytes)
        {
            throw new InvalidInputException(
                $"That file is {file.Length / 1024} KB; the limit is {options.Value.MaximumFileBytes / 1024} KB.");
        }

        await using var content = file.OpenReadStream();
        var batch = await shelfImporter.CreateBatchAsync(file.FileName, content, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, batch);
    }

    /// <summary>The most recent uploads, newest first.</summary>
    [HttpGet("batches")]
    public async Task<ActionResult<IReadOnlyList<ImportBatchResponse>>> GetRecentAsync(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 50)
        {
            throw new InvalidInputException("limit must be between 1 and 50.");
        }

        return Ok(await importQueryService.GetRecentBatchesAsync(limit, cancellationToken));
    }

    /// <summary>Everything the review page renders, in one round trip.</summary>
    [HttpGet("{batchId:guid}/queue")]
    public async Task<ActionResult<ImportQueueResponse>> GetQueueAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var queue = await importQueryService.GetQueueAsync(batchId, cancellationToken);
        return queue is null ? NotFound() : Ok(queue);
    }

    /// <summary>
    /// The review queue's manual fallback, for when none of the offered
    /// candidates is right.
    /// </summary>
    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<ImportCandidateResponse>>> SearchCandidatesAsync(
        [FromQuery(Name = "q")] string query,
        CancellationToken cancellationToken)
    {
        return Ok(await importQueryService.SearchCandidatesAsync(query, cancellationToken));
    }

    /// <summary>One decision about one series.</summary>
    // feature-spec §6 sketches this as PUT /api/import/rows/{id}. The unit is the
    // group, not the row: 13 Slime books are one decision, and asking for it 13
    // times would be the admin-afterthought version of this page.
    [HttpPut("groups/{groupId:guid}")]
    public async Task<ActionResult<ImportGroupResponse>> ResolveGroupAsync(
        Guid groupId,
        [FromBody] ResolveImportGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await importCommandService.ResolveGroupAsync(groupId, request, cancellationToken);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>Applies the batch to the shelf.</summary>
    [HttpPost("{batchId:guid}/commit")]
    public async Task<ActionResult<ImportCommitResponse>> CommitAsync(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var result = await importCommandService.CommitAsync(batchId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
