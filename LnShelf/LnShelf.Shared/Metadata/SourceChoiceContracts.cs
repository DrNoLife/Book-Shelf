namespace LnShelf.Shared.Metadata;

/// <summary>
/// Picks the source that decides one field of one series, after the sources disagreed.
/// </summary>
public sealed record ChooseSourceRequest
{
    /// <summary>VolumeCount, ReleaseDate or Status. SeriesLink cannot be chosen.</summary>
    public required SyncDiscrepancyField Field { get; init; }

    /// <summary>Set exactly when the field is per volume (ReleaseDate).</summary>
    public int? VolumeNumber { get; init; }

    public required MetadataSourceName Source { get; init; }
}

/// <summary>A pick the user has made, as the series page needs it for its quiet markers.</summary>
public sealed record SourceChoiceResponse
{
    public required SyncDiscrepancyField Field { get; init; }
    public int? VolumeNumber { get; init; }
    public required MetadataSourceName Source { get; init; }
}
