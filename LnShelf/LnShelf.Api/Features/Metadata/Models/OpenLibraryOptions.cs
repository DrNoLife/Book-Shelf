namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// Open Library client configuration, bound once through <c>IOptions&lt;T&gt;</c>.
/// </summary>
public sealed class OpenLibraryOptions
{
    public const string SectionName = "OpenLibrary";

    public Uri Endpoint { get; set; } = new("https://openlibrary.org/");

    /// <summary>Where cover bytes come from — a different host from the catalogue.</summary>
    public Uri CoverEndpoint { get; set; } = new("https://covers.openlibrary.org/");

    /// <summary>
    /// Who to contact about this traffic, appended to the User-Agent.
    /// </summary>
    // Open Library's docs ask for a contactable agent rather than a bare tool name,
    // and being identifiable is the price of using a free community catalogue
    // (CLAUDE.md — be a polite API citizen). Configuration rather than a constant so
    // the address is changeable without a rebuild.
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>
    /// Self-imposed, and well under the published ceiling.
    /// </summary>
    // Open Library's covers documentation caps non-id-keyed access at 100 requests
    // per 5 minutes — 20 a minute. Twelve leaves three quarters of that untouched,
    // which is the right side to err on for a single-user shelf running overnight
    // against somebody else's donated catalogue.
    public int RequestsPerMinute { get; set; } = 12;

    /// <summary>Search candidates read per volume during ISBN discovery.</summary>
    public int SearchResultLimit { get; set; } = 5;

    /// <summary>
    /// ISBN candidates verified per volume before discovery gives up on it.
    /// </summary>
    // One search can return a dozen editions of the same work across every market.
    // Verifying all of them would spend the politeness budget on a long tail that
    // measured 0-for-7 anyway (.research/volume-enrichment/e-isbn-first-pipeline.md).
    public int CandidateIsbnLimit { get; set; } = 8;

    /// <summary>
    /// Volumes one sync run will look up. A cap, not a target.
    /// </summary>
    // Enrichment retries the volumes it failed on, because Open Library's catalogue
    // grows and today's miss is next month's hit. That makes an uncapped run able to
    // spend an hour on gaps, so the run takes a bite and leaves the rest for
    // tomorrow.
    public int MaximumLookupsPerRun { get; set; } = 60;

    /// <summary>
    /// Volumes one on-demand refresh will look up — adding a series, or a manual sync.
    /// </summary>
    // Much smaller than the nightly allowance, because somebody is waiting for this one.
    // At twelve lookups a minute, eight is about forty seconds: slow for a click, but
    // inside the browser's default hundred-second request timeout with room to spare.
    // Whatever it does not reach, the nightly sweep finishes.
    public int MaximumLookupsOnDemand { get; set; } = 8;

    /// <summary>
    /// A day: an edition record that has existed for years is not about to change.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);
}
