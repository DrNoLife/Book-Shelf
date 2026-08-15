using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Politeness for Apple Books: a permit before every request, and a 429 honoured once
/// rather than hammered.
/// </summary>
public sealed class AppleBooksRateLimitHandler(
    AppleBooksRateLimiter rateLimiter,
    IOptions<AppleBooksOptions> options,
    ILogger<AppleBooksRateLimitHandler> logger) : DelegatingHandler
{
    private static readonly TimeSpan FallbackRetryAfter = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendWithPermitAsync(request, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var advertised = response.Headers.RetryAfter?.Delta ?? FallbackRetryAfter;
        var retryAfter = advertised <= TimeSpan.Zero || advertised > options.Value.MaximumRetryDelay
            ? options.Value.MaximumRetryDelay
            : advertised;

        logger.LogWarning(
            "Apple Books answered 429; waiting {RetryAfterSeconds}s before a single retry",
            retryAfter.TotalSeconds);

        response.Dispose();
        await Task.Delay(retryAfter, cancellationToken);

        return await SendWithPermitAsync(request, cancellationToken);
    }

    /// <summary>
    /// Waits for a permit, then applies the request timeout to the request alone.
    /// </summary>
    // The same arrangement as OpenLibraryRateLimitHandler's, and for the same reason:
    // HttpClient.Timeout covers the whole handler chain including this permit wait, so at
    // twenty requests a minute a queued call can sit longer than any sane per-request
    // timeout and fail for having waited rather than for having been slow. The client's
    // timeout is infinite and this owns it.
    private async Task<HttpResponseMessage> SendWithPermitAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                "The Apple Books request queue is full; no permit could be acquired.");
        }

        using var timeout = new CancellationTokenSource(options.Value.RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            return await base.SendAsync(request, linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested
                                                && !cancellationToken.IsCancellationRequested)
        {
            // Re-thrown as a timeout so the caller can tell "the source did not answer"
            // from "the run was cancelled". The catalogue pass treats it as one series'
            // failure either way, which is the point.
            throw new TimeoutException(
                $"Apple Books did not answer within {options.Value.RequestTimeout.TotalSeconds:F0}s.");
        }
    }
}
