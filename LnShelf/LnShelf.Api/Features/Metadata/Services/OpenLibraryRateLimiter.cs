using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The shared permit pool for Open Library requests. A singleton, for the same
/// reason as AniList's and MangaUpdates': a budget that resets per request is not a
/// budget.
/// </summary>
// Its own pool, and the smallest of the three. Open Library is a donated catalogue
// with a documented 100-requests-per-5-minutes ceiling on the covers side, and a
// nightly enrichment loop over a shelf of volumes is exactly the traffic shape that
// ceiling exists to stop.
public sealed class OpenLibraryRateLimiter : IDisposable
{
    private readonly SlidingWindowRateLimiter _rateLimiter;

    public OpenLibraryRateLimiter(IOptions<OpenLibraryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,

            // Deep enough to hold a whole run's worth of lookups: the queue is what
            // turns "too fast" into "slower" rather than into a failure.
            QueueLimit = options.Value.MaximumLookupsPerRun * 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
