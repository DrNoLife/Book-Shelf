namespace LnShelf.Api.Errors;

/// <summary>
/// An external metadata source refused to answer at all. A 503, not a bug here.
/// </summary>
// Its own type because the difference matters twice over. To the caller: "AniList has
// switched its API off" is not the same fact as "this service has a defect", and a 500
// with a swallowed message is what that difference used to look like — the client
// printed "net_http_message_not_success_statuscode_reason, 500".
//
// And to the search path: this is the exception a fallback to another source is allowed
// to act on. Being rate-limited is deliberately NOT one of these (see
// MetadataSourceFailure) — throttling is a request to wait, not an outage, and moving
// the traffic to somebody else's API would be the wrong answer to it.
public sealed class MetadataSourceUnavailableException(
    string sourceName,
    string upstreamMessage,
    int? upstreamStatusCode = null,
    Exception? innerException = null)
    : Exception(Describe(sourceName, upstreamMessage, upstreamStatusCode), innerException)
{
    /// <summary>
    /// The source that could not answer — or what was being asked, when every source
    /// failed and naming one of them would be half the story.
    /// </summary>
    public string SourceName { get; } = sourceName;

    /// <summary>
    /// What the source itself said, in its own words. Shown to the caller verbatim: it
    /// is the only part of this that tells them when to try again.
    /// </summary>
    public string UpstreamMessage { get; } = upstreamMessage;

    /// <summary>The HTTP status the source answered with, where there was one.</summary>
    public int? UpstreamStatusCode { get; } = upstreamStatusCode;

    private static string Describe(string sourceName, string upstreamMessage, int? upstreamStatusCode) =>
        upstreamStatusCode is { } statusCode
            ? $"{sourceName} is unavailable ({statusCode}): {upstreamMessage}"
            : $"{sourceName} is unavailable: {upstreamMessage}";
}
