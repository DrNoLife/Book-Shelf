namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// Apple Books (iTunes Search) client configuration, bound once through
/// <c>IOptions&lt;T&gt;</c>. Nothing here is secret: the search endpoint is keyless.
/// </summary>
public sealed class AppleBooksOptions
{
    public const string SectionName = "AppleBooks";

    /// <summary>Trailing slash on purpose — <c>HttpClient.BaseAddress</c> needs one.</summary>
    public Uri Endpoint { get; set; } = new("https://itunes.apple.com/");

    /// <summary>
    /// Apple's documented ceiling for the search endpoint is about 20 calls a minute.
    /// </summary>
    // Sitting on the published number rather than under it, unlike Open Library's 12
    // against 20: the catalogue costs one request per *series*, so a whole nightly sweep
    // over this shelf is single digits and never approaches the limit anyway.
    public int RequestsPerMinute { get; set; } = 20;

    /// <summary>
    /// Results asked for per series — Apple's own maximum.
    /// </summary>
    // A store search for a light-novel title returns the manga, the German editions, the
    // spin-offs and the audiobooks alongside the volumes wanted, so the wanted ones are a
    // minority of the response. Asking for fewer would drop real volumes off the end of a
    // long series rather than saving anything: it is one request either way.
    public int SearchLimit { get; set; } = 200;

    /// <summary>
    /// A day: a storefront listing for a book published years ago is not about to change.
    /// </summary>
    // The same reasoning as Open Library's, and the same practical effect: a sweep and an
    // on-demand refresh of the same series in one evening make one request between them.
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MaximumRetryDelay { get; set; } = TimeSpan.FromSeconds(60);
}
