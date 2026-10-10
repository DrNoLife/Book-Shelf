namespace LnShelf.Shared.Metadata;

/// <summary>Why the shelf is showing the value it shows for a disagreement.</summary>
[JsonConverter(typeof(StrictStringEnumConverter<SourceDisplayReason>))]
public enum SourceDisplayReason
{
    /// <summary>Nothing was changed and nobody has picked: the question is open.</summary>
    WaitingOnYou,

    /// <summary>The precedence list settled it.</summary>
    Automatic,

    /// <summary>The user picked.</summary>
    YourChoice,
}

/// <summary>
/// One recorded disagreement on a series, laid out side by side for the series page.
/// </summary>
public sealed record SourceConflictResponse
{
    public required SyncDiscrepancyField Field { get; init; }

    /// <summary>Set when the disagreement is about one volume.</summary>
    public int? VolumeNumber { get; init; }

    public required IReadOnlyList<SourceOptionResponse> Options { get; init; }

    /// <summary>The source whose value the shelf shows right now.</summary>
    public MetadataSourceName? DisplayedSource { get; init; }

    public required SourceDisplayReason Reason { get; init; }

    /// <summary>One plain sentence: why that source is the one shown.</summary>
    public required string Explanation { get; init; }

    /// <summary>False for SeriesLink, which is linked elsewhere.</summary>
    public required bool CanChoose { get; init; }

    /// <summary>True while nobody has settled it: the amber state.</summary>
    public required bool NeedsReview { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }

    public string? Note { get; init; }
}

/// <summary>One source's side of a disagreement, ready to print.</summary>
public sealed record SourceOptionResponse
{
    public required MetadataSourceName Source { get; init; }

    /// <summary>"Apple Books".</summary>
    public required string SourceName { get; init; }

    /// <summary>The value as recorded. Null when the source said nothing.</summary>
    public string? Value { get; init; }

    /// <summary>"9 Mar 2027", "in print", "18 volumes", "Completed", "nothing".</summary>
    public required string DisplayValue { get; init; }

    public required bool IsDisplayed { get; init; }
}
