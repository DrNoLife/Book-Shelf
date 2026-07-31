namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// What one run of the ISBN backfill did. Written so a second run can be seen to
/// have done nothing.
/// </summary>
public sealed record IsbnBackfillReport
{
    /// <summary>Volumes the staged rows named an ISBN for.</summary>
    public required int Offered { get; init; }

    public required int Written { get; init; }

    /// <summary>Already carrying an ISBN, so left alone — the second run's whole story.</summary>
    public required int AlreadyRecorded { get; init; }

    /// <summary>
    /// Staged ISBNs with no volume to land on: a row for a volume the shelf does not
    /// track, or a group that was matched but never committed.
    /// </summary>
    public required int WithoutVolume { get; init; }

    public string Summary =>
        $"{Offered} staged ISBNs · {Written} written · {AlreadyRecorded} already recorded · "
        + $"{WithoutVolume} with no volume to land on";
}
