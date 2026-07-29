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
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            InvalidInputException => (StatusCodes.Status400BadRequest, "Invalid input"),
            ConflictException => (StatusCodes.Status409Conflict, "Not allowed in this state"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected failure"),
        };

        // A 400 is the caller's problem and a normal event; a 500 is ours.
        if (statusCode is StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Refused {Method} {Path}: {Reason}",
                httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = title,

                // The message is safe to show for the cases we name; for a 500 it
                // could be anything, so it stays in the log.
                Detail = statusCode is StatusCodes.Status500InternalServerError ? null : exception.Message,
            },
        });
    }
}
