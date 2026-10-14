using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// MangaDex API configuration, bound once through <c>IOptions&lt;T&gt;</c>. Nothing here is
/// secret: the metadata endpoints are keyless.
/// </summary>
public sealed class MangaDexOptions
{
    public const string SectionName = "MangaDex";

    /// <summary>Trailing slash on purpose — <c>HttpClient.BaseAddress</c> needs one.</summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://api.mangadex.org/");

    /// <summary>
    /// A twentieth of MangaDex's global allowance of about five a second per address.
    /// </summary>
    // Far under the published number, like Open Library's: a chapter map is a nightly
    // nice-to-have, and MangaDex answers a 429 it keeps seeing with a temporary ban (a 403).
    [Range(1, 300)]
    public int RequestsPerMinute { get; set; } = 60;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Requests one sync run (or one on-demand refresh) may make. A cap, not a target.
    /// </summary>
    // A series costs four at most — two title searches, two aggregates — and two once its
    // MangaDex id is known; the cadence leaves most series alone on most nights. Sized for a
    // first sweep over a manga-heavy shelf.
    [Range(1, 10000)]
    public int MaximumRequestsPerRun { get; set; } = 200;
}
