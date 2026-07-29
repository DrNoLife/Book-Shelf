namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// What the matcher concluded about one series title.
/// </summary>
public sealed record SeriesMatch
{
    /// <summary>Best first; empty when the source knew nothing by that name.</summary>
    public required IReadOnlyList<ScoredSeriesCandidate> Candidates { get; init; }

    public required ImportGroupStatus Status { get; init; }
    public required ImportReviewReason Reason { get; init; }

    /// <summary>Set only when <see cref="Status"/> is <c>AutoMatched</c>.</summary>
    public int? MatchedAniListId { get; init; }

    public int? MatchScore { get; init; }
}

/// <summary>One candidate with its title similarity.</summary>
public sealed record ScoredSeriesCandidate
{
    public required SeriesSearchResult Result { get; init; }
    public required int Score { get; init; }
}
