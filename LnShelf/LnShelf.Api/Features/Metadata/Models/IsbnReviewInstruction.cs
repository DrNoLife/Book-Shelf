namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One reviewed, believed row: put this ISBN on this volume.
/// </summary>
// Deliberately smaller than the review file's row. Everything else in that file is
// evidence for the person reading it; only these three fields are an instruction, so
// only these three come back inward.
public sealed record IsbnReviewInstruction
{
    public required Guid SeriesId { get; init; }
    public required decimal VolumeNumber { get; init; }
    public required string Isbn { get; init; }
}
