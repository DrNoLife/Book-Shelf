namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// Writes for the import flow: the user's decisions, and applying them.
/// </summary>
public interface IImportCommandService
{
    /// <summary>
    /// Records one decision about one group — which series it is, whether to skip
    /// it, and whether its read volumes count as owned.
    /// </summary>
    /// <returns>Null when there is no such group.</returns>
    Task<ImportGroupResponse?> ResolveGroupAsync(
        Guid groupId,
        ResolveImportGroupRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies every matched group to the shelf: creates or updates series keyed
    /// on AniList id, adds volumes, writes volume and series user state.
    /// Additive — running it again for the same export creates nothing.
    /// </summary>
    /// <returns>Null when there is no such batch.</returns>
    Task<ImportCommitResponse?> CommitAsync(Guid batchId, CancellationToken cancellationToken);
}
