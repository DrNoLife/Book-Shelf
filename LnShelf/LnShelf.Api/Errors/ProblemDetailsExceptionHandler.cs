using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace LnShelf.Api.Errors;

/// <summary>
/// The central handler CLAUDE.md's error-handling rule assumes: services throw
/// and nothing catches on the way up, so this is the one place that turns an
/// exception into a response.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// What one exception becomes on the wire.
    /// </summary>
    // Public and static so the mapping can be asserted without hosting a request
    // pipeline: which status an exception earns is the whole behaviour of this class,
    // and it should not only ever be exercised by starting the app.
    public static (int StatusCode, string Title, string? Detail) Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            InvalidInputException => (StatusCodes.Status400BadRequest, "Invalid input", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Not allowed in this state", exception.Message),

            // Two requests racing to write the same row: the database's unique index caught
            // what the check-then-insert before it could not. Nothing is broken, so not a 500;
            // the message is ours, because Postgres's names an index nobody can act on.
            DbUpdateException { IsUniqueViolation: true } => (
                StatusCodes.Status409Conflict,
                "Changed at the same time",
                "This was changed at the same time somewhere else. Reload and try again."),

            // A request path that wraps a throttled source (release links): busy, not down.
            MetadataSourceUnavailableException { UpstreamStatusCode: StatusCodes.Status429TooManyRequests } busy => (
                StatusCodes.Status503ServiceUnavailable,
                $"{busy.SourceName} is busy",
                busy.UpstreamMessage),

            // The upstream's own words as the detail, rather than this app's wrapper
            // sentence: "The AniList API has been temporarily disabled due to severe
            // stability issues" is the only part a person can act on.
            MetadataSourceUnavailableException unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                $"{unavailable.SourceName} is unavailable",
                unavailable.UpstreamMessage),

            // Kestrel refusing the request itself, most often a body over a [RequestSizeLimit]
            // (a cover upload past 8 MB). It carries its own status (413 there), which is the
            // honest answer; it is not a server fault (audit 2026-10-09, #22).
            BadHttpRequestException badRequest => (
                badRequest.StatusCode,
                badRequest.StatusCode is StatusCodes.Status413PayloadTooLarge ? "Too large" : "Bad request",
                badRequest.StatusCode is StatusCodes.Status413PayloadTooLarge
                    ? "The upload is larger than the server accepts."
                    : "The request could not be read."),

            // A source throttling this app after the rate-limit handler has already waited and
            // retried once, or the handler's own queue being full. Neither is a defect, and both
            // pass with time, so they are a 503 and say "wait" (audit 2026-10-09, #20). The
            // titles stay generic: covers and avatars throw the same exceptions as the
            // metadata sources do.
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => (
                StatusCodes.Status503ServiceUnavailable,
                "A source is busy",
                "It is asking this app to slow down. Try again in a minute."),

            // The rate-limit handler's per-attempt deadline, and an exchange that never
            // happened (DNS, a refused connection) or an upstream 5xx: the same "having a bad
            // minute" ReleaseSourceUnavailable already answers with a 503, on every other
            // request path too. Any other 4xx stays a 500 below: a 400 or 401 from a source
            // means this app asked wrongly.
            TimeoutException
                or HttpRequestException { StatusCode: null }
                or HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError } => (
                StatusCodes.Status503ServiceUnavailable,
                "A source did not answer",
                "It did not answer just now. Try again in a moment."),

            // The message could be anything, so it stays in the log.
            _ => (StatusCodes.Status500InternalServerError, "Unexpected failure", null),
        };
    }

    /// <summary>
    /// How long the caller should wait before asking again, when that is known; null otherwise.
    /// </summary>
    // Only throttling has a known answer. Every source's budget is a one-minute sliding window
    // (SourceRateLimiter), so a minute is when permits are free again; the upstream's own
    // Retry-After is gone by the time the exception gets here, and the handler has already
    // waited it out once. A timeout or an outage says nothing about when it ends, so it gets
    // no header rather than an invented one. The cause chain is walked because request paths
    // wrap the 429 before it gets here: release links rethrow it as a
    // MetadataSourceUnavailableException carrying the status, and the search fallback wraps
    // MangaUpdates throttling it after AniList was already down.
    public static TimeSpan? RetryAfter(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return IsThrottling(exception) ? TimeSpan.FromMinutes(1) : null;
    }

    private static bool IsThrottling(Exception exception) =>
        exception is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests }
            or MetadataSourceUnavailableException { UpstreamStatusCode: StatusCodes.Status429TooManyRequests }
        || (exception.InnerException is { } inner && IsThrottling(inner));

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = Describe(exception);

        Log(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

        // Only on a 503: a defect that happens to wrap a 429 is still a 500, and "retry in a
        // minute" would be a promise about a bug.
        if (statusCode is StatusCodes.Status503ServiceUnavailable && RetryAfter(exception) is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
            },
        });
    }

    /// <summary>
    /// The request path as the log may hold it: a route that carries a secret token (the
    /// release calendar feed, the shared shelf) logs its route template instead, so
    /// <c>/api/calendar/{token}.ics</c> reaches the log and the token itself does not.
    /// </summary>
    // The token is a bearer secret: whoever reads the application log could otherwise open
    // the feed or the shelf. Every other route keeps its real path, which is what makes a
    // 500 traceable. The exception middleware clears the endpoint before calling handlers,
    // so the matched one is read from the feature it leaves behind.
    public static string LoggablePath(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint ?? httpContext.GetEndpoint();

        return endpoint is RouteEndpoint { RoutePattern: var pattern } && pattern.GetParameter("token") is not null
            ? "/" + (pattern.RawText ?? string.Empty).TrimStart('/')
            : httpContext.Request.Path.ToString();
    }

    /// <summary>
    /// A 400 is the caller's problem and a normal event; a 500 is ours; a 503 is
    /// neither, and is the one worth noticing in the log without paging anyone.
    /// </summary>
    private void Log(HttpContext httpContext, Exception exception, int statusCode)
    {
        switch (statusCode)
        {
            case StatusCodes.Status500InternalServerError:
                logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                    httpContext.Request.Method, LoggablePath(httpContext));
                break;

            case StatusCodes.Status503ServiceUnavailable:
                logger.LogWarning("Upstream unavailable on {Method} {Path}: {Reason}",
                    httpContext.Request.Method, LoggablePath(httpContext), exception.Message);
                break;

            default:
                logger.LogInformation("Refused {Method} {Path}: {Reason}",
                    httpContext.Request.Method, LoggablePath(httpContext), exception.Message);
                break;
        }
    }
}
