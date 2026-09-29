namespace LnShelf.Shared.Import;

/// <summary>
/// Where one import group — the books of a single series — has got to.
/// </summary>
// The unit is the group, not the row: a StoryGraph export lists volumes, and the
// thing being matched against AniList is the series they belong to. This is why
// feature-spec §5's per-row Resolution moved up a level.
[JsonConverter(typeof(StrictStringEnumConverter<ImportGroupStatus>))]
public enum ImportGroupStatus
{
    /// <summary>One candidate won by a clear margin; no human needed.</summary>
    AutoMatched,

    /// <summary>Waiting for the user. See <see cref="ImportReviewReason"/>.</summary>
    NeedsReview,

    /// <summary>The user picked a candidate.</summary>
    Resolved,

    /// <summary>The user chose to leave these books off the shelf.</summary>
    Skipped,

    /// <summary>Applied to the shelf.</summary>
    Committed,
}
