namespace LnShelf.Api.Features.Covers.Controllers;

[ApiController]
[Route("api/covers")]
public sealed class CoversController(ICoverStore coverStore) : ControllerBase
{
    /// <summary>
    /// Streams a cached cover. The client only ever hits this route — never an
    /// external CDN (design.md).
    /// </summary>
    // A catch-all because keys carry a prefix ("anilist/86355.jpg").
    [HttpGet("{**objectKey}")]
    public async Task<IActionResult> GetAsync(string objectKey, CancellationToken cancellationToken)
    {
        if (!CoverObjectKey.IsWellFormed(objectKey))
        {
            return BadRequest();
        }

        var cover = await coverStore.ReadAsync(objectKey, cancellationToken);
        if (cover is null)
        {
            return NotFound();
        }

        // Immutable: the key changes when the art does, so the browser can hold
        // onto this for a week and the grid stops re-requesting covers entirely.
        Response.Headers.CacheControl = "public, max-age=604800, immutable";

        return File(cover.Content, cover.ContentType);
    }
}
