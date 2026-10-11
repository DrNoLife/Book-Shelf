namespace LnShelf.Api.Features.Releases.Controllers;

/// <summary>Per-series notification mute, on the series page.</summary>
[ApiController]
[Route("api/series")]
public sealed class SeriesNotificationsController(
    ISeriesNotificationCommandService seriesNotificationCommandService) : ControllerBase
{
    /// <summary>Mutes or unmutes pushes about one series. Answers with the refreshed series.</summary>
    [HttpPut("{seriesId:guid}/notifications")]
    public async Task<ActionResult<SeriesDetailResponse>> SetMutedAsync(
        Guid seriesId,
        [FromBody] SetSeriesNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await seriesNotificationCommandService.SetMutedAsync(seriesId, request.Muted, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }
}
