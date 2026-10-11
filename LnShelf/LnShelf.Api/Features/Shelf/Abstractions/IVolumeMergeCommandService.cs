namespace LnShelf.Api.Features.Shelf.Abstractions;

/// <summary>
/// Folds one volume of a series into another: the user state of the first moves onto the
/// second, and the first is deleted. A repair for a volume that should never have existed —
/// a phantom made from a source's count that the reader marked meaning a bonus volume.
/// </summary>
public interface IVolumeMergeCommandService
{
    /// <exception cref="Errors.InvalidInputException">
    /// The series or either volume is not found, the title matches more than one series, the
    /// two numbers are the same, the target already carries user state, a source choice is
    /// pinned to the volume being merged away, or its uploaded cover is missing from the store.
    /// Nothing is written in any of those cases.
    /// </exception>
    Task<VolumeMergeReport> MergeAsync(VolumeMergeRequest request, CancellationToken cancellationToken);
}
