using System.Net;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Turns a failed release-source request made for a person into the 503 that says so.
/// </summary>
// The release source's client lets HttpRequestException escape, which is right for the sync —
// the per-series isolation catches it — but on a request somebody is waiting on it surfaced as
// a bare 500, saying "this app is broken" about MangaUpdates having a bad minute.
public static class ReleaseSourceUnavailable
{
    /// <summary>What a person reads when a source did not answer: this app's words, never the exception's.</summary>
    public const string DidNotAnswer = "It did not answer just now. Try again in a moment.";

    /// <summary>What a person reads when a source is throttling this app, or its request queue is full.</summary>
    public const string AskingToSlowDown = "It is asking this app to slow down. Try again in a minute.";

    public static MetadataSourceUnavailableException From(string sourceName, HttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Create(sourceName, exception.StatusCode is { } statusCode ? (int)statusCode : null, exception);
    }

    /// <summary>
    /// The source never finished answering: an HttpClient timeout, which surfaces as a cancelled
    /// task nobody asked to cancel (see <see cref="MetadataSourceFailure"/>), or a timeout proper.
    /// </summary>
    public static MetadataSourceUnavailableException FromTimeout(string sourceName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return Create(sourceName, statusCode: null, exception);
    }

    /// <summary>
    /// The sentence a person may read about a failed source request.
    /// </summary>
    // Only a MetadataSourceUnavailableException carries words meant for a reader (the source's
    // own, or one of the two sentences above). Any other exception's message can hold a host
    // name, a queue or a deadline, which belong in the log and never on the wire.
    public static string ReadableReason(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            MetadataSourceUnavailableException unavailable => unavailable.UpstreamMessage,
            HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => AskingToSlowDown,
            _ => DidNotAnswer,
        };
    }

    // A 429 (the source, or the rate-limit handler's own full queue) asks to wait, which is a
    // different instruction from "it is down", so the reader hears that instead.
    private static MetadataSourceUnavailableException Create(string sourceName, int? statusCode, Exception exception) =>
        new(
            sourceName,
            statusCode is StatusCodes.Status429TooManyRequests ? AskingToSlowDown : DidNotAnswer,
            statusCode,
            exception);
}
