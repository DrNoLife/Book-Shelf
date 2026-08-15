using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The shared permit pool for Apple Books requests. A singleton, for the same reason as
/// the other three: a budget that resets per request is not a budget.
/// </summary>
// Its own pool, because the four APIs have unrelated budgets. Apple documents roughly
// twenty search calls a minute, which is the number used here rather than a cautious
// fraction of it — the catalogue costs one request per series, so a whole sweep over this
// shelf is under ten and the ceiling is never the binding constraint.
public sealed class AppleBooksRateLimiter : IDisposable
{
    /// <summary>
    /// Deep enough for a whole sweep, which is one request per series.
    /// </summary>
    // A constant rather than an option: it is not a knob, it is the shape of the traffic.
    // Open Library's queue is sized from MaximumLookupsPerRun because its requests are
    // per volume and there is a matching option to read; this source has no such number.
    private const int QueuedRequestLimit = 64;

    private readonly SlidingWindowRateLimiter _rateLimiter;

    public AppleBooksRateLimiter(IOptions<AppleBooksOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = options.Value.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,

            // The queue is what turns "too fast" into "slower" rather than into a failure.
            QueueLimit = QueuedRequestLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) =>
        _rateLimiter.AcquireAsync(permitCount: 1, cancellationToken);

    public void Dispose() => _rateLimiter.Dispose();
}
