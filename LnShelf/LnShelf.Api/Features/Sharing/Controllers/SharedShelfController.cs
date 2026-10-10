using LnShelf.Api.Features.Sharing.Abstractions;

namespace LnShelf.Api.Features.Sharing.Controllers;

/// <summary>
/// A public shelf, read-only, for anyone with the link (feature-spec §4.8).
/// </summary>
// Anonymous on purpose, and the only anonymous endpoints besides login and health. Unknown,
// revoked and malformed tokens all answer the same 404, so the response never says whether a
// shelf ever existed behind a token.
[ApiController]
[Route("api/shared/{token}")]
[AllowAnonymous]
public sealed class SharedShelfController(ISharedShelfQueryService sharedShelf, ICoverStore coverStore) : ControllerBase
{
    /// <summary>The shelf. No-store, so neither a browser nor the edge keeps it past a revoke.</summary>
    [HttpGet]
    [EnableRateLimiting(SharingRateLimits.SharedShelf)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<SharedShelfResponse>> GetAsync(string token, CancellationToken cancellationToken) =>
        await sharedShelf.GetAsync(token, cancellationToken) is { } shelf ? Ok(shelf) : NotFound();

    /// <summary>Series art of a series on that shelf, and nothing else.</summary>
    // A catch-all because keys carry a prefix ("anilist/86355.jpg").
    [HttpGet("covers/{**objectKey}")]
    [EnableRateLimiting(SharingRateLimits.SharedCovers)]
    public async Task<IActionResult> GetCoverAsync(string token, string objectKey, CancellationToken cancellationToken)
    {
        // A per-volume key (an upload or a volume's synced art) is never public, so it is
        // refused before the token is even looked up. A malformed key is a 404 here rather
        // than the covers endpoint's 400, so this route answers every refusal the same way.
        if (!CoverObjectKey.IsWellFormed(objectKey)
            || CoverObjectKey.VolumeIdOf(objectKey) is not null
            || !await sharedShelf.IsCoverOnShelfAsync(token, objectKey, cancellationToken))
        {
            return NotFound();
        }

        var cover = await coverStore.ReadAsync(objectKey, cancellationToken);
        if (cover is null)
        {
            return NotFound();
        }

        // Private for the same reason as CoversController: no shared cache may keep it, so a
        // revoked link stops serving at the edge too.
        Response.Headers.CacheControl = "private, max-age=604800, immutable";

        return File(cover.Content, cover.ContentType);
    }
}
