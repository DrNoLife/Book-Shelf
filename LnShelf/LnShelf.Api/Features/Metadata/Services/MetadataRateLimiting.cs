namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Wires one named <c>HttpClient</c> to its own permit pool, and holds each source's
/// politeness numbers so the registration for it stays a single line.
/// </summary>
// The numbers live here rather than inside the limiter so that "which option sizes which
// queue" is answerable in one file instead of four. They are still read from each source's
// own options, so the four budgets remain independent (CLAUDE.md).
public static class MetadataRateLimiting
{
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Gives this client its own permit pool and the shared 429 handling.
        /// </summary>
        public IHttpClientBuilder AddSourcePoliteness(
            Func<IServiceProvider, SourceRateLimitPolicy> describePolicy)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(describePolicy);

            var clientName = builder.Name;

            // Keyed on the client's own name: one pool per named client, created once and
            // shared by every handler chain the factory rebuilds. The handler cannot own it —
            // a budget that resets when the chain is recycled is not a budget.
            builder.Services.AddKeyedSingleton(
                clientName,
                (provider, _) => new SourceRateLimiter(describePolicy(provider)));

            return builder.AddHttpMessageHandler(provider => new PoliteRateLimitHandler(
                provider.GetRequiredKeyedService<SourceRateLimiter>(clientName),
                provider.GetRequiredService<ILogger<PoliteRateLimitHandler>>()));
        }
    }

    /// <summary>
    /// AniList: 30 a minute while its API is degraded (CLAUDE.md), and — like every source
    /// now — the request timeout lives on the handler.
    /// </summary>
    public static SourceRateLimitPolicy AniList(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<AniListOptions>>().Value;

        return new SourceRateLimitPolicy
        {
            SourceName = "AniList",
            RequestsPerMinute = options.RequestsPerMinute,

            // One window's worth: past that, something is wrong with the caller.
            QueuedRequestLimit = options.RequestsPerMinute,

            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,
        };
    }

    /// <inheritdoc cref="AniList"/>
    public static SourceRateLimitPolicy MangaUpdates(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<MangaUpdatesOptions>>().Value;

        return new SourceRateLimitPolicy
        {
            SourceName = "MangaUpdates",
            RequestsPerMinute = options.RequestsPerMinute,
            QueuedRequestLimit = options.RequestsPerMinute,
            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,
        };
    }

    /// <summary>
    /// Open Library: the smallest budget, and the request timeout lives on the handler.
    /// </summary>
    public static SourceRateLimitPolicy OpenLibrary(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;

        return new SourceRateLimitPolicy
        {
            SourceName = "Open Library",
            RequestsPerMinute = options.RequestsPerMinute,

            // Deep enough to hold a whole run's worth of lookups, because its requests are
            // per volume and there is a matching option to size it from.
            QueuedRequestLimit = options.MaximumLookupsPerRun * 2,

            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,
        };
    }

    /// <inheritdoc cref="OpenLibrary"/>
    public static SourceRateLimitPolicy AppleBooks(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<AppleBooksOptions>>().Value;

        return new SourceRateLimitPolicy
        {
            SourceName = "Apple Books",
            RequestsPerMinute = options.RequestsPerMinute,

            // A constant rather than an option: it is not a knob, it is the shape of the
            // traffic. The catalogue costs one request per series, so a whole sweep over this
            // shelf is single digits and this is deep enough several times over.
            QueuedRequestLimit = 64,

            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,

            // Apple's rate-limit answer is a 403, not a 429.
            TreatsForbiddenAsRateLimited = true,
        };
    }

    /// <summary>
    /// MangaDex: a twentieth of its global allowance, and — as Apple's — a 403 is a rate limit.
    /// </summary>
    public static SourceRateLimitPolicy MangaDex(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<MangaDexOptions>>().Value;

        return new SourceRateLimitPolicy
        {
            SourceName = "MangaDex",
            RequestsPerMinute = options.RequestsPerMinute,

            // One window's worth: a run asks about one series at a time, four requests at most.
            QueuedRequestLimit = options.RequestsPerMinute,

            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,

            // MangaDex answers a caller that keeps going after a 429 with a temporary ban, a 403;
            // backing off on it is the only polite reading.
            TreatsForbiddenAsRateLimited = true,
        };
    }

    /// <summary>
    /// Wikipedia and Wikidata, one pool: a seventh of what Wikimedia allows a contactable agent,
    /// and the anonymous allowance when there is no contact address to send.
    /// </summary>
    public static SourceRateLimitPolicy Wikipedia(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<WikipediaOptions>>().Value;
        var contactEmail = provider.GetRequiredService<IOptions<OpenLibraryOptions>>().Value.ContactEmail;

        // Wikimedia allows an agent without contact details 10 requests a minute; going over it
        // would be impolite, whatever the configured number says.
        var requestsPerMinute = string.IsNullOrWhiteSpace(contactEmail)
            ? Math.Min(options.RequestsPerMinute, AnonymousWikimediaRequestsPerMinute)
            : options.RequestsPerMinute;

        return new SourceRateLimitPolicy
        {
            SourceName = "Wikipedia",
            RequestsPerMinute = requestsPerMinute,

            // One window's worth: a run asks about one series at a time, seven requests at most.
            QueuedRequestLimit = options.RequestsPerMinute,

            MaximumRetryDelay = options.MaximumRetryDelay,
            RequestTimeout = options.RequestTimeout,

            // Wikimedia's edge answers a client it has decided is scraping with a 403 that names
            // the robot policy. Backing off is the polite reading of that, as of a 429.
            TreatsForbiddenAsRateLimited = true,
        };
    }

    private const int AnonymousWikimediaRequestsPerMinute = 10;
}
