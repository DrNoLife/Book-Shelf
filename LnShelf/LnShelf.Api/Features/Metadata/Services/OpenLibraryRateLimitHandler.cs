using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Politeness for Open Library: a permit before every request, and a 429 honoured
/// once rather than hammered.
/// </summary>
public sealed class OpenLibraryRateLimitHandler(
    OpenLibraryRateLimiter rateLimiter,
    IOptions<OpenLibraryOptions> options,
    ILogger<OpenLibraryRateLimitHandler> logger) : DelegatingHandler
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
            "Open Library answered 429; waiting {RetryAfterSeconds}s before a single retry",
            retryAfter.TotalSeconds);

        response.Dispose();
        await Task.Delay(retryAfter, cancellationToken);

        return await SendWithPermitAsync(request, cancellationToken);
    }

    /// <summary>
    /// Waits for a permit, then applies the request timeout to the request alone.
    /// </summary>
    // The timeout is applied here rather than on the HttpClient, which is a real
    // difference from the AniList and MangaUpdates handlers. HttpClient.Timeout covers the
    // whole handler chain including this permit wait, and at twelve requests a minute a
    // queued lookup waits far longer than any sane per-request timeout — a 60-volume run
    // failed every request after the first few for exactly that reason. Their budgets are
    // 90 and 60 a minute, so their waits are around a second and the distinction never
    // bites; here it does, so the client's timeout is infinite and this owns it.
    private async Task<HttpResponseMessage> SendWithPermitAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                "The Open Library request queue is full; no permit could be acquired.");
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
            // from "the run was cancelled". The enrichment loop treats it as one volume's
            // failure either way, which is the point.
            throw new TimeoutException(
                $"Open Library did not answer within {options.Value.RequestTimeout.TotalSeconds:F0}s.");
        }
    }
}
