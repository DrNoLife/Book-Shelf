using LnShelf.Api.Features.Changelog.Mappers;

namespace LnShelf.Api.Features.Changelog.Controllers;

/// <summary>
/// Serves the release notes. The notes themselves — and the app version derived from
/// them — live in <see cref="ChangelogCatalog"/>, so shipping a release is a data edit
/// rather than a change to routing.
/// </summary>
[ApiController]
[Route("api/changelog")]
public sealed class ChangelogController : ControllerBase
{
    [HttpGet]
    public ActionResult<ChangelogResponse> Get() =>
        Ok(ChangelogMapper.ToResponse(ChangelogCatalog.Entries, AppVersion.Current));
}
