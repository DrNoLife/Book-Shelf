using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// AniList client configuration, bound once through <c>IOptions&lt;T&gt;</c>
/// (CLAUDE.md — Configuration). Nothing here is secret: AniList's public
/// GraphQL endpoint needs no credentials.
/// </summary>
// The annotations are checked at startup (ValidateDataAnnotations + ValidateOnStart in
// MetadataFeatureExtensions), so a bad value fails the boot with the field's name in the
// message instead of surfacing as a strange HTTP failure on the first search.
public sealed class AniListOptions
{
    public const string SectionName = "AniList";

    [Required]
    public Uri Endpoint { get; set; } = new("https://graphql.anilist.co/");

    /// <summary>AniList's published budget for anonymous clients.</summary>
    // The ceiling is a typo guard, not a policy: nothing this app talks to publishes a
    // budget anywhere near ten requests a second, so a larger number is a mistake.
    [Range(1, 600)]
    public int RequestsPerMinute { get; set; } = 90;

    /// <summary>Results asked for per search — 50 is AniList's own per-page maximum.</summary>
    [Range(1, 50)]
    public int SearchResultLimit { get; set; } = 20;

    /// <summary>Search-result cache lifetime (feature-spec §8).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Ceiling on an honoured <c>Retry-After</c>: a pathological value must not
    /// park a search request for an hour.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
