using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Which failures of a metadata source another source is allowed to answer for.
/// </summary>
// One copy of the rule, public and static so the matrix can be asserted directly: a
// fallback that fires on the wrong exception is how a rate limit turns into two APIs
// being hammered instead of one, and that is not a thing to find out in production.
//
// The principle is "the source did not answer" versus "the source answered, and the
// answer was about us". Only the first earns a second attempt elsewhere.
public static class MetadataSourceFailure
{
    /// <param name="cancellationToken">
    /// The caller's own token. A genuinely cancelled request is nobody's outage — the
    /// browser navigated away — and must not spend a second API's budget proving it.
    /// </param>
    public static bool WarrantsFallback(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return !cancellationToken.IsCancellationRequested && exception switch
        {
            // The source said, in words, that it is not serving.
            MetadataSourceUnavailableException => true,

            // HttpClient.Timeout surfaces as a cancelled task with nobody having asked for
            // cancellation, which the guard above has already ruled out. A source that
            // never finished answering has not answered.
            OperationCanceledException or TimeoutException => true,

            // Throttling is the source answering, about us. Waiting is the correct response
            // and the rate-limit handler has already done it once; moving the request to
            // another free community API instead would be impoliteness with extra steps.
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => false,

            // No status at all means no HTTP exchange happened — DNS, a refused connection,
            // a TLS failure. Every failure status other than 429 is thrown as the typed
            // exception above, so this branch is genuinely "unreachable", not "unhappy".
            HttpRequestException { StatusCode: null } => true,

            // Anything else — including the rate limiter's own "the queue is full" and a
            // GraphQL-level rejection of our query — is this app's problem to fix, not
            // another source's to paper over.
            _ => false,
        };
    }
}
