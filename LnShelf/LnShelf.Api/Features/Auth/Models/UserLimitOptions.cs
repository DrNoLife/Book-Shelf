using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Auth.Models;

/// <summary>
/// What one user may take of a shared server (feature-spec §7).
/// </summary>
// Sign-up is open, so these are the difference between a stranger's shelf and a stranger
// filling the disk or spending the metadata sources' patience for everyone. Generous for a
// real collection; low enough that abuse runs into them quickly.
public sealed class UserLimitOptions
{
    public const string SectionName = "Limits";

    [Range(1, 100_000)]
    public int MaximumSeriesPerUser { get; set; } = 500;

    /// <summary>Volume covers a user has uploaded and that are still in place.</summary>
    [Range(0, 100_000)]
    public int MaximumCoverOverridesPerUser { get; set; } = 300;

    /// <summary>
    /// Requests per minute, per user, to endpoints that call the metadata sources: search,
    /// add, import and its candidate search.
    /// </summary>
    [Range(1, 10_000)]
    public int ExternalRequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// Requests per minute, per user, for "More by {author}". Its own bucket because every
    /// series page asks, mostly answered from the cache, and browsing must not use up the
    /// search and add budget above.
    /// </summary>
    [Range(1, 10_000)]
    public int AuthorWorksRequestsPerMinute { get; set; } = 60;

    /// <summary>Whole-shelf "sync now" runs per hour, per user.</summary>
    [Range(1, 1_000)]
    public int SyncRunsPerHour { get; set; } = 2;
}
