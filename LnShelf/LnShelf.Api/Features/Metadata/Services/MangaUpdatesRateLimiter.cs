using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The shared permit pool for MangaUpdates requests. A singleton, for the same
/// reason as AniList's: a budget that resets per request is not a budget.
/// </summary>
// Its own pool rather than a shared one: the two APIs have unrelated budgets, and
// one nightly sync should not be able to spend AniList's on MangaUpdates' behalf.
public sealed class MangaUpdatesRateLimiter : IDisposable
{
    private readonly SlidingWindowRateLimiter _rateLimiter;

    public MangaUpdatesRateLimiter(IOptions<MangaUpdatesOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = options.Value.RequestsPerMinute,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
