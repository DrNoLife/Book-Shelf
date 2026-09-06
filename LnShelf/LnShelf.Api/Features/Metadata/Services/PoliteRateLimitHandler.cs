using System.Net;
using System.Net.Http.Headers;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Politeness, not resilience: every request waits for a permit before it leaves, and a 429
/// is honoured rather than hammered (CLAUDE.md — be a polite API citizen).
/// </summary>
// One handler where there were four near-identical ones. They differed in the source's name,
// the options type they read, and whether they bounded the request itself; all three are now
// the SourceRateLimitPolicy the limiter carries, so a fifth source is a registration rather
// than another copy of this file.
public sealed class PoliteRateLimitHandler(
    SourceRateLimiter rateLimiter,
    ILogger<PoliteRateLimitHandler> logger) : DelegatingHandler
{
    /// <summary>
    /// How long to wait after a 429: what the source asked for, capped by the policy's
    /// ceiling, and that ceiling outright when it asked for nothing usable.
    /// </summary>
    // One rule where there were two. AniList fell back to a hardcoded 60 seconds whenever
    // Retry-After was absent or non-positive — the non-positive case escaping its own
    // configured ceiling entirely — while the other three fell back to MaximumRetryDelay when
    // it was non-positive and to a hardcoded 30 seconds when it was absent. Same situation,
    // different wait, in four files.
    //
    // AniList's documented headers (X-RateLimit-Remaining and Retry-After) are the reference
    // for what a source is expected to say, so an advertised delay is still honoured. What
    // goes is the hardcoded constant sitting beside a configurable ceiling: with nothing
    // usable advertised, the wait is MaximumRetryDelay, which is per-source, already
    // configured, and already means "the longest this app is willing to wait".
    public static TimeSpan RetryDelayFor(RetryConditionHeaderValue? retryAfter, TimeSpan maximum)
    {
        if (retryAfter?.Delta is not { } advertised || advertised <= TimeSpan.Zero)
        {
            return maximum;
        }

        return advertised > maximum ? maximum : advertised;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendWithPermitAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var policy = rateLimiter.Policy;
        var retryAfter = RetryDelayFor(response.Headers.RetryAfter, policy.MaximumRetryDelay);

        logger.LogWarning(
            "{SourceName} answered 429; waiting {RetryAfterSeconds}s before a single retry",
            policy.SourceName,
            retryAfter.TotalSeconds);

        response.Dispose();
        await Task.Delay(retryAfter, cancellationToken);

        // One retry only. A second 429 means the budget is genuinely gone, and a request that
        // fails loudly beats a request queue that grows silently.
        return await SendWithPermitAsync(request, cancellationToken);
    }

    /// <summary>
    /// Waits for a permit, then sends — bounding the request itself when the policy says the
    /// <c>HttpClient</c> is not doing it.
    /// </summary>
    // The lease is time-based, so releasing it here does not hand the permit back: the window
    // does that. It is disposed early on purpose, before the response body is read.
    //
    // The per-request timeout belongs here rather than on the HttpClient for the slow sources.
    // HttpClient.Timeout covers the whole handler chain including the permit wait, and at
    // twelve requests a minute a queued lookup waits far longer than any sane per-request
    // timeout — a 60-volume run failed every request after the first few for exactly that
    // reason. AniList and MangaUpdates run at 90 and 60 a minute, so their waits are around a
    // second and the distinction never bites; their clients keep their own timeout and their
    // policies carry none.
    private async Task<HttpResponseMessage> SendWithPermitAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var policy = rateLimiter.Policy;

        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                $"The {policy.SourceName} request queue is full; no permit could be acquired.");
        }

        if (policy.RequestTimeout is not { } requestTimeout)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        using var timeout = new CancellationTokenSource(requestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            return await base.SendAsync(request, linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested
                                                && !cancellationToken.IsCancellationRequested)
        {
            // Re-thrown as a timeout so the caller can tell "the source did not answer" from
            // "the run was cancelled". The loop above treats it as one item's failure either
            // way, which is the point.
            throw new TimeoutException(
                $"{policy.SourceName} did not answer within {requestTimeout.TotalSeconds:F0}s.");
        }
    }
}
