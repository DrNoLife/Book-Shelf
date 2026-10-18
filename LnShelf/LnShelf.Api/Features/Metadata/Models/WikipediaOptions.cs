using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// Wikipedia and Wikidata API configuration, bound once through <c>IOptions&lt;T&gt;</c>.
/// Nothing here is secret: both APIs are keyless.
/// </summary>
// No contact address of its own. Wikimedia's User-Agent policy asks for one, and the shelf
// already keeps exactly one for that purpose (OpenLibraryOptions.ContactEmail); a second
// setting holding the same address would only be a second place to forget it.
public sealed class WikipediaOptions
{
    public const string SectionName = "Wikipedia";

    /// <summary>The English Wikipedia action API, where the chapter list pages live.</summary>
    [Required]
    public Uri Endpoint { get; set; } = new("https://en.wikipedia.org/w/api.php");

    /// <summary>The Wikidata action API, which links an AniList id to a series' items.</summary>
    [Required]
    public Uri WikidataEndpoint { get; set; } = new("https://www.wikidata.org/w/api.php");

    /// <summary>
    /// Self-imposed, and far under what Wikimedia allows an identified caller.
    /// </summary>
    // Wikimedia allows 200 a minute to an agent with contact details and 10 to one without
    // (mediawiki.org, Wikimedia APIs/Rate limits). MetadataRateLimiting.Wikipedia drops to the
    // anonymous 10 when no contact address is configured, whatever this says.
    [Range(1, 200)]
    public int RequestsPerMinute { get; set; } = 30;

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Requests one sync run (or one on-demand refresh) may make. A cap, not a target.
    /// </summary>
    // A known page costs one request; finding one costs up to seven (WikipediaListPageFinder),
    // and only once per series. Sized like MangaDex's, for a first sweep over a manga shelf.
    [Range(1, 10000)]
    public int MaximumRequestsPerRun { get; set; } = 200;
}
