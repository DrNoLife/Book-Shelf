using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// MangaUpdates client configuration, bound once through <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class MangaUpdatesOptions
{
    public const string SectionName = "MangaUpdates";

    [Required]
    public Uri Endpoint { get; set; } = new("https://api.mangaupdates.com/v1/");

    /// <summary>
    /// MangaUpdates publishes no rate limit, so this is a self-imposed one.
    /// </summary>
    // Sixty a minute — one a second — is deliberately conservative: an unpublished
    // limit is not an absent limit, and a nightly job over a few hundred series has
    // all night (CLAUDE.md — be a polite API citizen).
    [Range(1, 600)]
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Candidates a search returns, every one of which is scored.
    /// </summary>
    // There is deliberately no second, smaller "how many to score" limit. There was
    // one, set to three, and it silently lost the correct match for 86―EIGHTY-SIX:
    // MangaUpdates returns it fourth, so it was never fetched and never scored, and
    // the series was reported as low-confidence against an unrelated Chinese novel.
    // A candidate the search returns is a candidate the matcher looks at.
    [Range(1, 100)]
    public int SearchResultLimit { get; set; } = 5;

    /// <summary>Minimum title similarity for an automatic link.</summary>
    // Bounded by the scorer, which is documented as producing 0-100 (TitleSimilarityScorer).
    [Range(0, 100)]
    public int AutoLinkMinimumScore { get; set; } = 92;

    /// <summary>How far ahead of the runner-up an automatic link must be.</summary>
    [Range(0, 100)]
    public int AutoLinkMinimumMargin { get; set; } = 8;

    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);
}
