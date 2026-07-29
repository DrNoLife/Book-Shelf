using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Politeness, not resilience: every AniList request waits for a permit before
/// it leaves, and a 429 is honoured rather than hammered.
/// </summary>
public sealed class AniListRateLimitHandler(
    AniListRateLimiter rateLimiter,
    IOptions<AniListOptions> options,
    ILogger<AniListRateLimitHandler> logger) : DelegatingHandler
{
    /// <summary>AniList's window, used when a 429 carries no <c>Retry-After</c>.</summary>
    private static readonly TimeSpan FallbackRetryAfter = TimeSpan.FromSeconds(60);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendWithPermitAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var retryAfter = ReadRetryAfter(response);
        logger.LogWarning(
            "AniList answered 429; waiting {RetryAfterSeconds}s before a single retry",
            retryAfter.TotalSeconds);

        response.Dispose();
        await Task.Delay(retryAfter, cancellationToken);

        // One retry only. A second 429 means the budget is genuinely gone, and a
        // search that fails loudly beats a request queue that grows silently.
        return await SendWithPermitAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithPermitAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The lease is time-based, so releasing it here does not hand the permit
        // back — the window does that. It is disposed early on purpose, before
        // the response body is read.
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                "The AniList request queue is full; no permit could be acquired.");
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private TimeSpan ReadRetryAfter(HttpResponseMessage response)
    {
        var advertised = response.Headers.RetryAfter?.Delta ?? FallbackRetryAfter;
        var maximum = options.Value.MaximumRetryDelay;

        return advertised switch
        {
            _ when advertised <= TimeSpan.Zero => FallbackRetryAfter,
            _ when advertised > maximum => maximum,
            _ => advertised,
        };
    }
}
