using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The nightly sync's schedule.
/// </summary>
public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>
    /// Whether the nightly job runs at all. Off by default.
    /// </summary>
    // Deliberately opt-in: in development the app starts many times a day, and a
    // sync on every start would spend two community APIs' budgets to learn nothing.
    // POST /api/sync is the same code path, on demand.
    public bool Enabled { get; set; }

    /// <summary>Local hour of day, 0–23. Default 04:00 (feature-spec §4.2).</summary>
    // Validated rather than trusted: this arrives as the `Sync__Hour` environment variable
    // in the container, and a typo used to bind cleanly and then throw
    // ArgumentOutOfRangeException from inside the background loop — which .NET answers by
    // stopping the whole host, hours after the mistake, with an opaque trace.
    [Range(0, 23)]
    public int Hour { get; set; } = 4;
}
