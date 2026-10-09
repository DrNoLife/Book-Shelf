namespace LnShelf.Api.Features.Changelog.Controllers;

/// <summary>
/// The one endpoint open without signing in: is the API up, can it reach its database,
/// and which version is it. deploy.ps1 polls it after a deploy.
/// </summary>
// Here rather than in a slice of its own because the version is the Changelog slice's
// fact. It says nothing about the shelf, so it is safe to leave open.
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController(LnShelfDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { version = AppVersion.Current, database = "unreachable" });
        }

        return Ok(new { version = AppVersion.Current, database = "ok" });
    }
}
