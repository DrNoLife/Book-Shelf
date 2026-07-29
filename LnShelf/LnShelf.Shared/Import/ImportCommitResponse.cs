namespace LnShelf.Shared.Import;

/// <summary>
/// What committing a batch actually did. Counted separately because "additive"
/// is a claim that has to be checkable: a second commit of the same export
/// creates nothing and updates everything.
/// </summary>
public sealed record ImportCommitResponse
{
    public required Guid BatchId { get; init; }
    public required int SeriesCreated { get; init; }
    public required int SeriesUpdated { get; init; }

    /// <summary>
    /// Volumes the metadata source did not know about, added as local-only
    /// (feature-spec §4.1's escape hatch).
    /// </summary>
    public required int LocalOnlyVolumesCreated { get; init; }

    public required int VolumeStatesWritten { get; init; }
    public required int GroupsCommitted { get; init; }
    public required int GroupsSkipped { get; init; }
}
