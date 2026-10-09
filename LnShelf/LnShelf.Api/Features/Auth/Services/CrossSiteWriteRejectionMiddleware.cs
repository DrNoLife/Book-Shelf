namespace LnShelf.Api.Features.Auth.Services;

/// <summary>
/// Refuses a write to the API that the browser says came from another site.
/// </summary>
// Second line of defence against cross-site request forgery. The session cookie is
// SameSite=Lax, which already keeps it off cross-site POSTs; Sec-Fetch-Site covers the
// rest without antiforgery tokens in every client write. A missing header (old browsers,
// curl) is let through, because Lax still applies there.
public sealed class CrossSiteWriteRejectionMiddleware(RequestDelegate next, IProblemDetailsService problemDetails)
{
    public static bool ShouldReject(string method, string? secFetchSite) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        && string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api")
            && ShouldReject(context.Request.Method, context.Request.Headers["Sec-Fetch-Site"]))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = { Status = StatusCodes.Status403Forbidden, Title = "Cross-site request refused" },
            });
            return;
        }

        await next(context);
    }
}
