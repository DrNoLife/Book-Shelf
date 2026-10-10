namespace LnShelf.Api.Features.Metadata.Abstractions;

/// <summary>Reads for the series page's side-by-side view of what each source said.</summary>
public interface ISourceConflictQueryService
{
    /// <returns>Null when no series has that id; empty when the sources agree on everything.</returns>
    Task<IReadOnlyList<SourceConflictResponse>?> GetAsync(Guid seriesId, CancellationToken cancellationToken);
}
