namespace LnShelf.Shared.Import;

/// <summary>
/// One decision about one group: which series it is, or that it should be left
/// off the shelf.
/// </summary>
public sealed record ResolveImportGroupRequest
{
    /// <summary>
    /// The chosen series. Null together with <see cref="Skip"/> false only makes
    /// sense for changing <see cref="TreatReadAsOwned"/> on a group that is
    /// already matched.
    /// </summary>
    public int? AniListId { get; init; }

    /// <summary>Leave these books off the shelf entirely.</summary>
    public bool Skip { get; init; }

    /// <summary>
    /// Whether read volumes count as owned for this group (feature-spec §4.4's
    /// decision, made correctable here).
    /// </summary>
    public bool TreatReadAsOwned { get; init; } = true;
}
