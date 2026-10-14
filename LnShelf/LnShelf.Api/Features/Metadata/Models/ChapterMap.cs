namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>One volume's chapters: from the first to the last, or open-ended while the last is unknown.</summary>
public sealed record VolumeChapterRange(int VolumeNumber, decimal FirstChapter, decimal? LastChapter);

/// <summary>
/// What ChapterMapBuilder made of one source's answer: ranges to write, or the reason none
/// can be trusted.
/// </summary>
public sealed record ChapterMap
{
    /// <summary>The ranges, in volume order. Empty when rejected.</summary>
    public IReadOnlyList<VolumeChapterRange> Ranges { get; init; } = [];

    /// <summary>Why the whole map was refused. Null for a usable map.</summary>
    public string? RejectedBecause { get; init; }

    public bool IsUsable => RejectedBecause is null;

    public static ChapterMap Rejected(string reason) => new() { RejectedBecause = reason };
}

/// <summary>
/// What the builder needs to know about the shelf's side of a series to judge a map.
/// </summary>
/// <param name="TrackedWholeVolumes">Whole volumes the shelf tracks, released or not.</param>
/// <param name="ReleasedWholeVolumes">The numbers of the whole volumes that are out.</param>
/// <param name="LatestChapter">The release source's latest chapter, when it knows one.</param>
/// <param name="IsCompleted">The series' publication status is Completed, so its highest volume closes.</param>
public sealed record ChapterMapShelf(
    int TrackedWholeVolumes,
    IReadOnlySet<int> ReleasedWholeVolumes,
    decimal? LatestChapter,
    bool IsCompleted);
