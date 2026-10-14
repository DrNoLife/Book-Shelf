namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>One volume's range as it will be written, and the source it is credited to.</summary>
public sealed record MergedVolumeRange(VolumeChapterRange Range, MetadataSourceName Source);

/// <summary>One source's range for a volume, as offered to the merge.</summary>
public sealed record SourcedVolumeRange(MetadataSourceName Source, VolumeChapterRange Range);

/// <summary>
/// Two or more sources starting one volume on chapters more than one apart.
/// </summary>
/// <param name="Offers">Every source's range for the volume, in precedence order.</param>
/// <param name="WrittenSource">
/// The source whose range the shelf shows: precedence's, unless it broke the order. Null when no
/// offer rose above the volume before it, so the volume was left without a range.
/// </param>
public sealed record ChapterStartDisagreement(
    int VolumeNumber,
    IReadOnlyList<SourcedVolumeRange> Offers,
    MetadataSourceName? WrittenSource);

/// <summary>What ChapterMapMerger made of every usable map: the ranges to write and the disagreements to record.</summary>
public sealed record MergedChapterMap(
    IReadOnlyList<MergedVolumeRange> Ranges,
    IReadOnlyList<ChapterStartDisagreement> Disagreements);
