namespace LnShelf.Api.Features.Preferences.Controllers;

[ApiController]
[Route("api/preferences")]
public sealed class PreferencesController(IPreferencesService preferencesService) : ControllerBase
{
    /// <summary>The single preferences row (feature-spec §4.6).</summary>
    [HttpGet]
    public async Task<ActionResult<PreferencesResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await preferencesService.GetAsync(cancellationToken));

    /// <summary>Partial update — every omitted setting is left alone.</summary>
    [HttpPut]
    public async Task<ActionResult<PreferencesResponse>> UpdateAsync(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await preferencesService.UpdateAsync(request, cancellationToken));
}
