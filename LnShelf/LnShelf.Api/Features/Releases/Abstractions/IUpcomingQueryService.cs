namespace LnShelf.Api.Features.Releases.Abstractions;

/// <summary>
/// Reads for the Upcoming view. It has no writes: buying a volume is a Shelf command.
/// </summary>
public interface IUpcomingQueryService
{
    /// <param name="format">Null means every format — LN and manga are different budgets, so the view filters.</param>
    Task<UpcomingResponse> GetUpcomingAsync(Format? format, CancellationToken cancellationToken);
}
