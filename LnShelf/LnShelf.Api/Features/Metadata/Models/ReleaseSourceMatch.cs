namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What linking one series to the release source concluded.
/// </summary>
public sealed record ReleaseSourceMatch
{
    public required ReleaseMatchOutcome Outcome { get; init; }

    /// <summary>Set only when <see cref="Outcome"/> is <c>Linked</c>.</summary>
    public long? MatchedExternalId { get; init; }

    /// <summary>The winning candidate's detail, when there was one worth fetching.</summary>
    public ReleaseSeriesInfo? MatchedSeries { get; init; }

    /// <summary>Title similarity of the best candidate, 0–100.</summary>
    public int? Score { get; init; }

    /// <summary>
    /// One line a person can act on: what was found and why it was not accepted.
    /// </summary>
    public required string Explanation { get; init; }

    public bool IsLinked => Outcome is ReleaseMatchOutcome.Linked && MatchedExternalId is not null;
}
