using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// The matcher's thresholds, bound once through <c>IOptions&lt;T&gt;</c>. They are
/// configuration rather than constants because the right numbers are only
/// knowable against a real export.
/// </summary>
// The ranges are validated on start (ImportFeatureExtensions). A typo in an
// environment variable is a boot-time failure that names the setting, not a
// matcher that quietly accepts everything or offers nothing — every one of these
// numbers is a threshold whose wrong value looks like working software.
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>Candidates kept per group for the review queue to show.</summary>
    // Zero would leave the queue with nothing to click; twenty is already more
    // rows than a person reads before scrolling.
    [Range(1, 20)]
    public int CandidatesPerGroup { get; set; } = 5;

    /// <summary>Below this, the best candidate is never accepted unseen.</summary>
    [Range(0, 100)]
    public int AutoMatchMinimumScore { get; set; } = 92;

    /// <summary>
    /// How far ahead of the runner-up the winner must be. This is the threshold
    /// that sends a light novel and its identically-titled manga to review
    /// instead of picking one.
    /// </summary>
    [Range(0, 100)]
    public int AutoMatchMinimumMargin { get; set; } = 8;

    /// <summary>Below this, a candidate is not worth showing at all.</summary>
    [Range(0, 100)]
    public int CandidateMinimumScore { get; set; } = 55;

    /// <summary>Upload guards. A StoryGraph export is tens of kilobytes.</summary>
    // Floored at a kilobyte so a mistyped zero cannot refuse every real file, and
    // capped well under the controller's own 8 MB request limit's order of
    // magnitude: raising this past what the framework will accept would only move
    // the refusal somewhere less legible.
    [Range(1024, 64 * 1024 * 1024)]
    public long MaximumFileBytes { get; set; } = 8 * 1024 * 1024;

    [Range(1, 100_000)]
    public int MaximumRowCount { get; set; } = 10_000;

    /// <summary>
    /// The most series one upload may contain.
    /// </summary>
    // The row cap alone did not bound the expensive part: every series costs an AniList
    // search and a few cover fetches, one after another in a single request, so 10,000
    // one-book series was 10,000 searches. 300 is several times a large real shelf, and
    // past it the request would outlive the proxies in front of the API anyway.
    [Range(1, 10_000)]
    public int MaximumGroupCount { get; set; } = 300;
}
