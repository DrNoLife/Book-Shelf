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

            // The upstream's own words as the detail, rather than this app's wrapper
            // sentence: "The AniList API has been temporarily disabled due to severe
            // stability issues" is the only part a person can act on.
            MetadataSourceUnavailableException unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                $"{unavailable.SourceName} is unavailable",
                unavailable.UpstreamMessage),

            // The message could be anything, so it stays in the log.
            _ => (StatusCodes.Status500InternalServerError, "Unexpected failure", null),
        };
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = Describe(exception);

        Log(httpContext, exception, statusCode);

        httpContext.Response.StatusCode = statusCode;

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
    /// A 400 is the caller's problem and a normal event; a 500 is ours; a 503 is
    /// neither, and is the one worth noticing in the log without paging anyone.
    /// </summary>
    private void Log(HttpContext httpContext, Exception exception, int statusCode)
    {
        switch (statusCode)
        {
            case StatusCodes.Status500InternalServerError:
                logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                break;

            case StatusCodes.Status503ServiceUnavailable:
                logger.LogWarning("Upstream unavailable on {Method} {Path}: {Reason}",
                    httpContext.Request.Method, httpContext.Request.Path, exception.Message);
                break;

            default:
                logger.LogInformation("Refused {Method} {Path}: {Reason}",
                    httpContext.Request.Method, httpContext.Request.Path, exception.Message);
                break;
        }
    }
}
