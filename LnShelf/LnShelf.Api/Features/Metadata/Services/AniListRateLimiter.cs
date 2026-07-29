using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The shared permit pool for AniList requests. A singleton, because a budget
/// that resets per request is not a budget (CLAUDE.md — be a polite API citizen).
/// </summary>
// Sliding window rather than fixed: a fixed window allows 90 requests in the last
// second of one window and 90 more in the first second of the next, which is 180
// in two seconds from AniList's point of view. Six segments smooth that out.
public sealed class AniListRateLimiter : IDisposable
{
    private readonly SlidingWindowRateLimiter _rateLimiter;

    public AniListRateLimiter(IOptions<AniListOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,

            // Requests wait their turn rather than failing. The queue is bounded
            // at one window's worth: past that, something is wrong with the
            // caller, not with AniList.
            QueueLimit = options.Value.RequestsPerMinute,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
