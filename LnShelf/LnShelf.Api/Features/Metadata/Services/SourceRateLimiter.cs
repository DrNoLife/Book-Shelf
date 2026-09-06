using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// One source's shared permit pool. A singleton per source, because a budget that resets
/// per request is not a budget (CLAUDE.md — be a polite API citizen).
/// </summary>
// Sliding window rather than fixed: a fixed window allows a whole minute's worth of requests
// in the last second of one window and a whole minute's worth more in the first second of the
// next, which is double the agreed rate from the source's point of view. Six segments smooth
// that out.
//
// One class where there were four identical ones — AniList, MangaUpdates, Open Library and
// Apple Books each had a copy differing only in which options type it read and how it sized
// its queue. The four *budgets* are still independent: every source registers its own
// instance with its own policy. Only the mechanism is shared.
public sealed class SourceRateLimiter : IDisposable
{
    private readonly SlidingWindowRateLimiter _rateLimiter;

    public SourceRateLimiter(SourceRateLimitPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        Policy = policy;

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = policy.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,

            // Requests wait their turn rather than failing. Past the queue's depth, something
            // is wrong with the caller rather than with the source.
            QueueLimit = policy.QueuedRequestLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    /// <summary>
    /// The numbers this pool was built from, carried so the handler in front of it does not
    /// need its own copy of them.
    /// </summary>
    public SourceRateLimitPolicy Policy { get; }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
