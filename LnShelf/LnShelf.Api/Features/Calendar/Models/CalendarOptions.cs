using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Calendar.Models;

/// <summary>
/// The anonymous release calendar feed's rate limit, per client address (feature-spec §4.9).
/// </summary>
// A load guard like SharingOptions, not the security: the token's 128 bits are. Calendar
// apps poll every few hours at most, so the default is generous for them and still keeps
// the endpoint from being a cheap way to make the server query.
public sealed class CalendarOptions
{
    public const string SectionName = "Calendar";

    /// <summary>Feed fetches per minute, per client address.</summary>
    [Range(1, 10_000)]
    public int RequestsPerMinute { get; set; } = 30;
}
