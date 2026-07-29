namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// The matcher's thresholds, bound once through <c>IOptions&lt;T&gt;</c>. They are
/// configuration rather than constants because the right numbers are only
/// knowable against a real export.
/// </summary>
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>Candidates kept per group for the review queue to show.</summary>
    public int CandidatesPerGroup { get; set; } = 5;

    /// <summary>Below this, the best candidate is never accepted unseen.</summary>
    public int AutoMatchMinimumScore { get; set; } = 92;

    /// <summary>
    /// How far ahead of the runner-up the winner must be. This is the threshold
    /// that sends a light novel and its identically-titled manga to review
    /// instead of picking one.
    /// </summary>
    public int AutoMatchMinimumMargin { get; set; } = 8;

    /// <summary>Below this, a candidate is not worth showing at all.</summary>
    public int CandidateMinimumScore { get; set; } = 55;

    /// <summary>Upload guards. A StoryGraph export is tens of kilobytes.</summary>
    public long MaximumFileBytes { get; set; } = 8 * 1024 * 1024;

    public int MaximumRowCount { get; set; } = 10_000;
}
