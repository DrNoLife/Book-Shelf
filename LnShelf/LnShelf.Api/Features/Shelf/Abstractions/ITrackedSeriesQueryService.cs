namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Which external series the current user already tracks. A read for the search page.
/// </summary>
public interface ITrackedSeriesQueryService
{
    Task<TrackedSeriesIdsResponse> GetTrackedIdsAsync(CancellationToken cancellationToken);
}
