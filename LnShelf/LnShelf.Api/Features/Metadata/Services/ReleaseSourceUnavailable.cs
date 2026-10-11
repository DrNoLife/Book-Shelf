namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Turns a failed release-source request made for a person into the 503 that says so.
/// </summary>
// The release source's client lets HttpRequestException escape, which is right for the sync —
// the per-series isolation catches it — but on a request somebody is waiting on it surfaced as
// a bare 500, saying "this app is broken" about MangaUpdates having a bad minute.
public static class ReleaseSourceUnavailable
{
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

    private static MetadataSourceUnavailableException Create(string sourceName, int? statusCode, Exception exception) =>
        new(sourceName, "It did not answer just now. Try again in a moment.", statusCode, exception);
}
