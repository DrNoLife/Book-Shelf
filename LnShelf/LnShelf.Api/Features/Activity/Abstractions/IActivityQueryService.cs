using LnShelf.Shared.Activity;

namespace LnShelf.Api.Features.Activity.Abstractions;

/// <summary>
/// Reads "Since your last visit" from the shelf's activity log.
/// </summary>
public interface IActivityQueryService
{
    /// <summary>
    /// The events since the reader last dismissed the panel, or from a fixed window back when
    /// they never have.
    /// </summary>
    Task<ActivityResponse> GetActivityAsync(CancellationToken cancellationToken);
}
