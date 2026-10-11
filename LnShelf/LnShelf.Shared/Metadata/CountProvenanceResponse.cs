namespace LnShelf.Shared.Metadata;

/// <summary>
/// How the series' published-volume count was arrived at: what "How is this counted?" explains.
/// </summary>
public sealed record CountProvenanceResponse
{
    /// <summary>Null when the count predates provenance being recorded.</summary>
    public VolumeCountBasis? Basis { get; init; }

    /// <summary>The text the count was read from, verbatim, when there was one.</summary>
    public string? Evidence { get; init; }

    /// <summary>The series' last sync, which is when the count was last checked. Null if never synced.</summary>
    public DateTimeOffset? CheckedAt { get; init; }
}
