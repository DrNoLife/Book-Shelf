namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>Reads for the series-detail page.</summary>
public interface ISeriesDetailQueryService
{
    /// <returns>Null when there is no such series.</returns>
    Task<SeriesDetailResponse?> GetAsync(Guid seriesId, CancellationToken cancellationToken);
}
