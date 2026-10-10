using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Sharing.Models;

/// <summary>
/// Rate limits on the two anonymous shared-shelf endpoints, per client address (feature-spec §4.8).
/// </summary>
// A load guard, not the security: the token's 128 bits are what make guessing pointless.
// These only keep an open endpoint from becoming a cheap way to make the server work.
public sealed class SharingOptions
{
    public const string SectionName = "Sharing";

    /// <summary>Shared-shelf page loads per minute, per client address.</summary>
    [Range(1, 10_000)]
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>
    /// Shared-shelf cover requests per minute, per client address. High enough that a
    /// shelf at the series limit loads every cover in one go.
    /// </summary>
    [Range(1, 100_000)]
    public int CoverRequestsPerMinute { get; set; } = 600;
}
