namespace LnShelf.Api.Features.Covers.Controllers;

[ApiController]
[Route("api/covers")]
public sealed class CoversController(ICoverStore coverStore, LnShelfDbContext dbContext) : ControllerBase
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

        // A volume's cover is its owner's: under the owner filter another user's volume does
        // not exist, so its cover is a 404 like anything else of theirs. Series art keyed by a
        // source id is shared catalogue art and needs no check.
        if (CoverObjectKey.VolumeIdOf(objectKey) is { } volumeId
            && !await dbContext.Volumes.AnyAsync(volume => volume.Id == volumeId, cancellationToken))
        {
            return NotFound();
        }

        var cover = await coverStore.ReadAsync(objectKey, cancellationToken);
        if (cover is null)
        {
            return NotFound();
        }

        // Immutable: the key changes when the art does, so the browser can hold
        // onto this for a week and the grid stops re-requesting covers entirely.
        // Private: the browser may keep it for a week (the key changes when the art does), but
        // no shared cache may, because the Cloudflare edge would hand a user's cover to anyone
        // asking for that URL without the owner check above.
        Response.Headers.CacheControl = "private, max-age=604800, immutable";

        return File(cover.Content, cover.ContentType);
    }
}
