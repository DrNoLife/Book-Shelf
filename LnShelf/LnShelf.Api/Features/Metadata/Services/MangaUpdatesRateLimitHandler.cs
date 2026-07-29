using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Politeness for MangaUpdates: a permit before every request, and a 429 honoured
/// once rather than hammered.
/// </summary>
public sealed class MangaUpdatesRateLimitHandler(
    MangaUpdatesRateLimiter rateLimiter,
    IOptions<MangaUpdatesOptions> options,
    ILogger<MangaUpdatesRateLimitHandler> logger) : DelegatingHandler
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
            "MangaUpdates answered 429; waiting {RetryAfterSeconds}s before a single retry",
            retryAfter.TotalSeconds);

        response.Dispose();
        await Task.Delay(retryAfter, cancellationToken);

        return await SendWithPermitAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithPermitAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var lease = await rateLimiter.AcquireAsync(cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException(
                "The MangaUpdates request queue is full; no permit could be acquired.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
