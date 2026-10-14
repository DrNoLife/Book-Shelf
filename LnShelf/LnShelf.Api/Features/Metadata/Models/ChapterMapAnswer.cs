namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>How a chapter-map source answered for one series.</summary>
public enum ChapterMapAnswerKind
{
    /// <summary>The source matched the series and listed chapters per volume.</summary>
    Found,

    /// <summary>The source has no record it can tie to this series.</summary>
    Unmatched,

    /// <summary>The source matched the series, but its volumes cannot be used (a manhwa's "volumes" are seasons).</summary>
    Rejected,

    /// <summary>The source was not asked, because its request allowance for this run is spent.</summary>
    Skipped,
}

/// <summary>
/// One chapter-map source's answer for one series, in the source-neutral shape the builder
/// reads: whole volume number to the chapter numbers the source files under it.
/// </summary>
// Raw rather than ranged on purpose. Every source's lists carry strays and gaps, and the rules
// that turn lists into ranges are the shelf's, not the source's (ChapterMapBuilder) — so a
// second source is a new implementation of the seam, never a second copy of those rules.
public sealed record ChapterMapAnswer
{
    public required ChapterMapAnswerKind Kind { get; init; }

    /// <summary>
    /// Whole volume number (1 and up) to the chapters filed under it. Empty unless
    /// <see cref="Kind"/> is Found.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<decimal>> ChaptersByVolume { get; init; } =
        new Dictionary<int, IReadOnlyList<decimal>>();

    /// <summary>Why the answer is Rejected or Unmatched, for the log. Null otherwise.</summary>
    public string? Reason { get; init; }

    public static ChapterMapAnswer Unmatched(string reason) => new() { Kind = ChapterMapAnswerKind.Unmatched, Reason = reason };

    public static ChapterMapAnswer Rejected(string reason) => new() { Kind = ChapterMapAnswerKind.Rejected, Reason = reason };

    public static ChapterMapAnswer Skipped { get; } = new() { Kind = ChapterMapAnswerKind.Skipped };

    public static ChapterMapAnswer Found(IReadOnlyDictionary<int, IReadOnlyList<decimal>> chaptersByVolume) =>
        new() { Kind = ChapterMapAnswerKind.Found, ChaptersByVolume = chaptersByVolume };
}
