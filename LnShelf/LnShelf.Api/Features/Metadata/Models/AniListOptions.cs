namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// AniList client configuration, bound once through <c>IOptions&lt;T&gt;</c>
/// (CLAUDE.md — Configuration). Nothing here is secret: AniList's public
/// GraphQL endpoint needs no credentials.
/// </summary>
public sealed class AniListOptions
{
    public const string SectionName = "AniList";

    public Uri Endpoint { get; set; } = new("https://graphql.anilist.co/");

    /// <summary>AniList's published budget for anonymous clients.</summary>
    public int RequestsPerMinute { get; set; } = 90;

    public int SearchResultLimit { get; set; } = 20;

    /// <summary>Search-result cache lifetime (feature-spec §8).</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Ceiling on an honoured <c>Retry-After</c>: a pathological value must not
    /// park a search request for an hour.
    /// </summary>
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
