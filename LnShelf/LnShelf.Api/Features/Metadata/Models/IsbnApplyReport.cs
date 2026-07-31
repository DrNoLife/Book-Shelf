namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What one run of the apply command did. Written so a second run can be seen to
/// have done nothing.
/// </summary>
public sealed record IsbnApplyReport
{
    /// <summary>Rows in the file that named a volume and an ISBN.</summary>
    public required int Reviewed { get; init; }

    public required int Written { get; init; }

    /// <summary>Already carrying an ISBN, so left alone — the second run's whole story.</summary>
    public required int AlreadyRecorded { get; init; }

    /// <summary>Rows naming a volume the shelf does not track.</summary>
    public required int WithoutVolume { get; init; }

    public string Summary =>
        $"{Reviewed} reviewed ISBNs · {Written} written · {AlreadyRecorded} already recorded · "
        + $"{WithoutVolume} naming no tracked volume";
}
